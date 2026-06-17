using Npgsql;
using TorneoAmigos.Models;

namespace TorneoAmigos.Data
{
    public class EncuestasRepository
    {
        private readonly string _connectionString;
        public EncuestasRepository(IConfiguration cfg) =>
            _connectionString = cfg.GetConnectionString("TorneoAmigosDB")
                ?? throw new InvalidOperationException("Connection string not found.");

        private NpgsqlConnection GetConnection() => new(_connectionString);

        public List<Encuesta> GetEncuestasActivas()
        {
            var encuestas = new Dictionary<int, Encuesta>();
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT e.id, e.pregunta, e.tipo, e.activa, e.max_votos, e.temporada_id,
                       eo.id, eo.texto, eo.equipo_id, COALESCE(eq.pais_code, eq2.pais_code, ''), eo.orden,
                       COUNT(ev.id) as votos
                FROM encuestas e
                LEFT JOIN encuesta_opciones eo ON eo.encuesta_id = e.id
                LEFT JOIN equipos eq ON eo.equipo_id = eq.id
                LEFT JOIN equipos eq2 ON eo.equipo_id IS NULL AND LOWER(eo.texto) = LOWER(eq2.nombre)
                LEFT JOIN encuesta_votos ev ON ev.opcion_id = eo.id
                WHERE e.activa = true
                GROUP BY e.id, e.pregunta, e.tipo, e.activa, e.max_votos, e.temporada_id,
                         eo.id, eo.texto, eo.equipo_id, eq.pais_code, eq2.pais_code, eo.orden
                ORDER BY e.id DESC, eo.orden ASC", conn);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int encId = r.GetInt32(0);
                if (!encuestas.ContainsKey(encId))
                    encuestas[encId] = new Encuesta
                    {
                        Id = encId, Pregunta = r.GetString(1), Tipo = r.GetString(2),
                        Activa = r.GetBoolean(3), MaxVotos = r.GetInt32(4),
                        TemporadaId = r.IsDBNull(5) ? null : r.GetInt32(5)
                    };

                if (!r.IsDBNull(6))
                {
                    var votos = Convert.ToInt32(r.GetInt64(11));
                    encuestas[encId].Opciones.Add(new EncuestaOpcion
                    {
                        Id = r.GetInt32(6), EncuestaId = encId,
                        Texto = r.GetString(7),
                        EquipoId = r.IsDBNull(8) ? null : r.GetInt32(8),
                        FlagCode = string.IsNullOrEmpty(r.GetString(9)) ? null : r.GetString(9),
                        Orden = r.GetInt32(10),
                        Votos = votos
                    });
                    encuestas[encId].TotalVotos += votos;
                }
            }
            // Deduplicate total votes (counted per opcion, not per encuesta)
            foreach (var enc in encuestas.Values)
                enc.TotalVotos = enc.Opciones.Sum(o => o.Votos);

            return encuestas.Values.ToList();
        }

        public List<int> GetVotosDelUsuario(int encuestaId, string autor)
        {
            var ids = new List<int>();
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT opcion_id FROM encuesta_votos
                WHERE encuesta_id = @E AND LOWER(autor) = LOWER(@A)", conn);
            cmd.Parameters.AddWithValue("@E", encuestaId);
            cmd.Parameters.AddWithValue("@A", autor.Trim());
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read()) ids.Add(r.GetInt32(0));
            return ids;
        }

        public bool Votar(int encuestaId, int opcionId, string autor)
        {
            if (string.IsNullOrWhiteSpace(autor) || autor.Length > 50) return false;

            // Verificar max_votos
            using var conn = GetConnection();
            conn.Open();
            using (var chk = new NpgsqlCommand(@"
                SELECT e.max_votos, COUNT(ev.id)
                FROM encuestas e
                LEFT JOIN encuesta_votos ev ON ev.encuesta_id = e.id AND LOWER(ev.autor) = LOWER(@A)
                WHERE e.id = @E
                GROUP BY e.max_votos", conn))
            {
                chk.Parameters.AddWithValue("@E", encuestaId);
                chk.Parameters.AddWithValue("@A", autor.Trim());
                using var r = chk.ExecuteReader();
                if (r.Read())
                {
                    int maxVotos = r.GetInt32(0);
                    int yaVoto   = Convert.ToInt32(r.GetInt64(1));
                    if (yaVoto >= maxVotos) return false;
                }
            }

            using var cmd = new NpgsqlCommand(@"
                INSERT INTO encuesta_votos (encuesta_id, opcion_id, autor)
                VALUES (@E, @O, @A)
                ON CONFLICT DO NOTHING", conn);
            cmd.Parameters.AddWithValue("@E", encuestaId);
            cmd.Parameters.AddWithValue("@O", opcionId);
            cmd.Parameters.AddWithValue("@A", autor.Trim());
            return cmd.ExecuteNonQuery() > 0;
        }

        // Admin: crear encuesta automática de descensos/ascensos para la temporada activa
        public int CrearEncuestaAutomatica(int temporadaId, string tipo, List<Equipo> equipos)
        {
            // tipo: 'descenso_primera' o 'ascenso_b'
            string pregunta = tipo == "descenso_primera"
                ? "⬇️ ¿Quiénes crees que descenderán de Primera División esta temporada? (elegí hasta 2)"
                : "⬆️ ¿Quiénes crees que ascenderán de Primera Nacional? (elegí hasta 2)";
            int maxVotos = 2;

            using var conn = GetConnection();
            conn.Open();

            // Verificar que no existe ya para esta temporada y tipo
            using (var chk = new NpgsqlCommand(
                "SELECT COUNT(*) FROM encuestas WHERE temporada_id = @T AND pregunta LIKE @P", conn))
            {
                chk.Parameters.AddWithValue("@T", temporadaId);
                chk.Parameters.AddWithValue("@P", pregunta.Substring(0, 10) + "%");
                if (Convert.ToInt32(chk.ExecuteScalar()) > 0) return 0;
            }

            int encId;
            using (var ins = new NpgsqlCommand(@"
                INSERT INTO encuestas (pregunta, tipo, activa, max_votos, temporada_id)
                VALUES (@P, 'equipos', true, @M, @T) RETURNING id", conn))
            {
                ins.Parameters.AddWithValue("@P", pregunta);
                ins.Parameters.AddWithValue("@M", maxVotos);
                ins.Parameters.AddWithValue("@T", temporadaId);
                encId = Convert.ToInt32(ins.ExecuteScalar());
            }

            for (int i = 0; i < equipos.Count; i++)
            {
                using var opt = new NpgsqlCommand(@"
                    INSERT INTO encuesta_opciones (encuesta_id, texto, equipo_id, orden)
                    VALUES (@E, @T, @EQ, @O)", conn);
                opt.Parameters.AddWithValue("@E",  encId);
                opt.Parameters.AddWithValue("@T",  equipos[i].Nombre);
                opt.Parameters.AddWithValue("@EQ", equipos[i].Id);
                opt.Parameters.AddWithValue("@O",  i);
                opt.ExecuteNonQuery();
            }
            return encId;
        }

        public void ToggleEncuesta(int id, bool activa)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand("UPDATE encuestas SET activa = @A WHERE id = @Id", conn);
            cmd.Parameters.AddWithValue("@A",  activa);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            cmd.ExecuteNonQuery();
        }

        public void EliminarEncuesta(int id)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand("DELETE FROM encuestas WHERE id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            cmd.ExecuteNonQuery();
        }

        public List<Encuesta> GetTodasLasEncuestas()
        {
            var encuestas = new Dictionary<int, Encuesta>();
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT e.id, e.pregunta, e.tipo, e.activa, e.max_votos, e.temporada_id,
                       eo.id, eo.texto, eo.equipo_id, COALESCE(eq.pais_code, eq2.pais_code, ''), eo.orden,
                       COUNT(ev.id) as votos
                FROM encuestas e
                LEFT JOIN encuesta_opciones eo ON eo.encuesta_id = e.id
                LEFT JOIN equipos eq ON eo.equipo_id = eq.id
                LEFT JOIN equipos eq2 ON eo.equipo_id IS NULL AND LOWER(eo.texto) = LOWER(eq2.nombre)
                LEFT JOIN encuesta_votos ev ON ev.opcion_id = eo.id
                GROUP BY e.id, e.pregunta, e.tipo, e.activa, e.max_votos, e.temporada_id,
                         eo.id, eo.texto, eo.equipo_id, eq.pais_code, eq2.pais_code, eo.orden
                ORDER BY e.id DESC, eo.orden ASC", conn);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int encId = r.GetInt32(0);
                if (!encuestas.ContainsKey(encId))
                    encuestas[encId] = new Encuesta {
                        Id = encId, Pregunta = r.GetString(1), Tipo = r.GetString(2),
                        Activa = r.GetBoolean(3), MaxVotos = r.GetInt32(4),
                        TemporadaId = r.IsDBNull(5) ? null : r.GetInt32(5)
                    };
                if (!r.IsDBNull(6))
                {
                    var votos = Convert.ToInt32(r.GetInt64(11));
                    encuestas[encId].Opciones.Add(new EncuestaOpcion {
                        Id = r.GetInt32(6), EncuestaId = encId, Texto = r.GetString(7),
                        EquipoId = r.IsDBNull(8) ? null : r.GetInt32(8),
                        FlagCode = string.IsNullOrEmpty(r.GetString(9)) ? null : r.GetString(9),
                        Orden = r.GetInt32(10), Votos = votos
                    });
                }
            }
            foreach (var enc in encuestas.Values)
                enc.TotalVotos = enc.Opciones.Sum(o => o.Votos);
            return encuestas.Values.ToList();
        }

        public int CrearEncuestaManual(string pregunta, int maxVotos, int? temporadaId, List<string> opciones)
        {
            using var conn = GetConnection();
            conn.Open();
            int encId;
            using (var ins = new NpgsqlCommand(@"
                INSERT INTO encuestas (pregunta, tipo, activa, max_votos, temporada_id)
                VALUES (@P, 'opciones', true, @M, @T) RETURNING id", conn))
            {
                ins.Parameters.AddWithValue("@P", pregunta);
                ins.Parameters.AddWithValue("@M", maxVotos);
                ins.Parameters.AddWithValue("@T", (object?)temporadaId ?? DBNull.Value);
                encId = Convert.ToInt32(ins.ExecuteScalar());
            }
            for (int i = 0; i < opciones.Count; i++)
            {
                using var opt = new NpgsqlCommand(@"
                    INSERT INTO encuesta_opciones (encuesta_id, texto, orden)
                    VALUES (@E, @T, @O)", conn);
                opt.Parameters.AddWithValue("@E", encId);
                opt.Parameters.AddWithValue("@T", opciones[i].Trim());
                opt.Parameters.AddWithValue("@O", i);
                opt.ExecuteNonQuery();
            }
            return encId;
        }
    }
}
