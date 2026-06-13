using Npgsql;
using TorneoAmigos.Models;

namespace TorneoAmigos.Data
{
    public class PrediccionesRepository
    {
        private readonly string _connectionString;

        public PrediccionesRepository(IConfiguration cfg) =>
            _connectionString = cfg.GetConnectionString("TorneoAmigosDB")
                ?? throw new InvalidOperationException("Connection string not found.");

        private NpgsqlConnection GetConnection() => new(_connectionString);

        public List<Prediccion> GetPrediccionesPorPartido(int partidoId, int divisionId)
        {
            var lista = new List<Prediccion>();
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id, partido_id, division_id, autor, pais_flag, prediccion_1x2,
                       goles_local, goles_visitante, puntos, created_at
                FROM predicciones
                WHERE partido_id = @P AND division_id = @D
                ORDER BY created_at ASC", conn);
            cmd.Parameters.AddWithValue("@P", partidoId);
            cmd.Parameters.AddWithValue("@D", divisionId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(MapPrediccion(r));
            return lista;
        }

        public bool GuardarPrediccion(int partidoId, int divisionId, string autor, string? paisFlag,
            string prediccion1x2, int? golesLocal, int? golesVisitante)
        {
            if (string.IsNullOrWhiteSpace(autor) || autor.Length > 50) return false;
            if (!new[] { "L", "E", "V" }.Contains(prediccion1x2)) return false;

            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO predicciones (partido_id, division_id, autor, pais_flag, prediccion_1x2, goles_local, goles_visitante)
                VALUES (@P, @D, @A, @F, @PX, @GL, @GV)
                ON CONFLICT (partido_id, division_id, autor)
                DO UPDATE SET prediccion_1x2 = @PX, goles_local = @GL, goles_visitante = @GV, created_at = NOW()", conn);
            cmd.Parameters.AddWithValue("@P", partidoId);
            cmd.Parameters.AddWithValue("@D", divisionId);
            cmd.Parameters.AddWithValue("@A", autor.Trim());
            cmd.Parameters.AddWithValue("@F", (object?)paisFlag ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PX", prediccion1x2);
            cmd.Parameters.AddWithValue("@GL", (object?)golesLocal ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@GV", (object?)golesVisitante ?? DBNull.Value);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        // Se llama al guardar el resultado real de un partido — calcula puntos de todas las predicciones
        public void CalcularPuntosPartido(int partidoId, int divisionId, int golesLocalReal, int golesVisitanteReal)
        {
            string resultadoReal = golesLocalReal > golesVisitanteReal ? "L"
                                  : golesLocalReal < golesVisitanteReal ? "V" : "E";

            using var conn = GetConnection();
            conn.Open();

            // Traer todas las predicciones de ese partido
            var preds = new List<(int id, string p1x2, int? gl, int? gv)>();
            using (var cmd = new NpgsqlCommand(@"
                SELECT id, prediccion_1x2, goles_local, goles_visitante
                FROM predicciones WHERE partido_id = @P AND division_id = @D", conn))
            {
                cmd.Parameters.AddWithValue("@P", partidoId);
                cmd.Parameters.AddWithValue("@D", divisionId);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    preds.Add((r.GetInt32(0), r.GetString(1),
                        r.IsDBNull(2) ? null : r.GetInt32(2),
                        r.IsDBNull(3) ? null : r.GetInt32(3)));
            }

            foreach (var (id, p1x2, gl, gv) in preds)
            {
                int puntos = 0;
                if (gl.HasValue && gv.HasValue && gl == golesLocalReal && gv == golesVisitanteReal)
                    puntos = 3; // resultado exacto
                else if (p1x2 == resultadoReal)
                    puntos = 1; // 1X2 correcto

                using var upd = new NpgsqlCommand("UPDATE predicciones SET puntos = @Pts WHERE id = @Id", conn);
                upd.Parameters.AddWithValue("@Pts", puntos);
                upd.Parameters.AddWithValue("@Id", id);
                upd.ExecuteNonQuery();
            }
        }

        public List<RankingPronosticador> GetRanking(int limit = 20)
        {
            var lista = new List<RankingPronosticador>();
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT autor,
                       MAX(pais_flag) as pais_flag,
                       COALESCE(SUM(puntos), 0) as total_puntos,
                       COUNT(*) as total_predicciones,
                       COUNT(*) FILTER (WHERE puntos >= 1) as aciertos_1x2,
                       COUNT(*) FILTER (WHERE puntos = 3) as aciertos_exactos
                FROM predicciones
                WHERE puntos IS NOT NULL
                GROUP BY autor
                ORDER BY total_puntos DESC, aciertos_exactos DESC
                LIMIT @L", conn);
            cmd.Parameters.AddWithValue("@L", limit);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new RankingPronosticador
                {
                    Autor           = r.GetString(0),
                    PaisFlag        = r.IsDBNull(1) ? null : r.GetString(1),
                    TotalPuntos     = r.GetInt32(2),
                    Predicciones    = Convert.ToInt32(r.GetInt64(3)),
                    Aciertos1X2     = Convert.ToInt32(r.GetInt64(4)),
                    AciertosExactos = Convert.ToInt32(r.GetInt64(5))
                });
            }
            return lista;
        }

        public Prediccion? GetMiPrediccion(int partidoId, int divisionId, string autor)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT id, partido_id, division_id, autor, pais_flag, prediccion_1x2,
                       goles_local, goles_visitante, puntos, created_at
                FROM predicciones
                WHERE partido_id = @P AND division_id = @D AND LOWER(autor) = LOWER(@A)", conn);
            cmd.Parameters.AddWithValue("@P", partidoId);
            cmd.Parameters.AddWithValue("@D", divisionId);
            cmd.Parameters.AddWithValue("@A", autor.Trim());
            conn.Open();
            using var r = cmd.ExecuteReader();
            return r.Read() ? MapPrediccion(r) : null;
        }

        private static Prediccion MapPrediccion(System.Data.IDataReader r) => new()
        {
            Id              = r.GetInt32(0),
            PartidoId       = r.GetInt32(1),
            DivisionId      = r.GetInt32(2),
            Autor           = r.GetString(3),
            PaisFlag        = r.IsDBNull(4) ? null : r.GetString(4),
            Prediccion1X2   = r.GetString(5),
            GolesLocal      = r.IsDBNull(6) ? null : r.GetInt32(6),
            GolesVisitante  = r.IsDBNull(7) ? null : r.GetInt32(7),
            Puntos          = r.IsDBNull(8) ? null : r.GetInt32(8),
            CreatedAt       = r.GetDateTime(9)
        };
    }
}
