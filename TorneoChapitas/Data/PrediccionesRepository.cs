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
            if (!new[] { "L", "V" }.Contains(prediccion1x2)) return false;

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

        public List<(string TemporadaNombre, int Puntos, int Aciertos1X2, int AciertosExactos, int Total)> GetHistorialPronosticador(string autor)
        {
            const string sql = @"
                SELECT t.nombre,
                       COALESCE(SUM(p.puntos), 0) as puntos,
                       COUNT(*) FILTER (WHERE pa.jugado = true AND (
                           (p.prediccion_1x2 = 'L' AND pa.goleslocal > pa.golesvisitante) OR
                           (p.prediccion_1x2 = 'V' AND pa.golesvisitante > pa.goleslocal)
                       )) as aciertos_1x2,
                       COUNT(*) FILTER (WHERE pa.jugado = true AND
                           p.goles_local = pa.goleslocal AND p.goles_visitante = pa.golesvisitante
                       ) as aciertos_exacto,
                       COUNT(*) as total
                FROM predicciones p
                JOIN partidos pa ON p.partido_id = pa.id
                JOIN fechas f ON pa.fechaid = f.id
                JOIN temporadas t ON f.temporada_id = t.id
                WHERE LOWER(TRIM(p.autor)) = LOWER(TRIM(@A))
                GROUP BY t.id, t.nombre, t.numero
                ORDER BY t.numero DESC";
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@A", autor.Trim());
            conn.Open();
            using var r = cmd.ExecuteReader();
            var lista = new List<(string, int, int, int, int)>();
            while (r.Read())
                lista.Add((r.GetString(0), Convert.ToInt32(r.GetInt64(1)),
                    Convert.ToInt32(r.GetInt64(2)), Convert.ToInt32(r.GetInt64(3)),
                    Convert.ToInt32(r.GetInt64(4))));
            return lista;
        }

        public List<RankingPronosticador> GetRankingPorTemporada(int temporadaId)
        {
            const string sql = @"
                SELECT p.autor,
                       COALESCE(SUM(p.puntos), 0) as puntos,
                       COUNT(*) FILTER (WHERE pa.jugado = true AND (
                           (p.prediccion_1x2 = 'L' AND pa.goleslocal > pa.golesvisitante) OR
                           (p.prediccion_1x2 = 'V' AND pa.golesvisitante > pa.goleslocal)
                       )) as aciertos_1x2,
                       COUNT(*) FILTER (WHERE pa.jugado = true AND
                           p.goles_local = pa.goleslocal AND p.goles_visitante = pa.golesvisitante
                       ) as aciertos_exacto
                FROM predicciones p
                JOIN partidos pa ON p.partido_id = pa.id
                JOIN fechas f ON pa.fechaid = f.id
                WHERE f.temporada_id = @T
                GROUP BY p.autor
                ORDER BY puntos DESC, aciertos_1x2 DESC
                LIMIT 20";
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@T", temporadaId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            var lista = new List<RankingPronosticador>();
            while (r.Read())
                lista.Add(new RankingPronosticador
                {
                    Autor = r.GetString(0),
                    TotalPuntos = Convert.ToInt32(r.GetInt64(1)),
                    Aciertos1X2 = Convert.ToInt32(r.GetInt64(2)),
                    AciertosExactos = Convert.ToInt32(r.GetInt64(3))
                });
            return lista;
        }

        public List<RankingPronosticador> GetRanking(int limit = 20) =>
            GetRankingPorDivision(null, limit);

        public List<RankingPronosticador> GetRankingPorDivision(int? divisionId, int limit = 20)
        {
            var lista = new List<RankingPronosticador>();
            var where = divisionId.HasValue ? "WHERE puntos IS NOT NULL AND division_id = @D" : "WHERE puntos IS NOT NULL";
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand($@"
                SELECT autor,
                       MAX(pais_flag) as pais_flag,
                       COALESCE(SUM(puntos), 0) as total_puntos,
                       COUNT(*) as total_predicciones,
                       COUNT(*) FILTER (WHERE puntos >= 1) as aciertos_1x2,
                       COUNT(*) FILTER (WHERE puntos = 3) as aciertos_exactos
                FROM predicciones
                {where}
                GROUP BY autor
                ORDER BY total_puntos DESC, aciertos_exactos DESC
                LIMIT @L", conn);
            if (divisionId.HasValue) cmd.Parameters.AddWithValue("@D", divisionId.Value);
            cmd.Parameters.AddWithValue("@L", limit);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                lista.Add(new RankingPronosticador
                {
                    Autor           = r.GetString(0),
                    PaisFlag        = r.IsDBNull(1) ? null : r.GetString(1),
                    TotalPuntos     = r.GetInt32(2),
                    Predicciones    = Convert.ToInt32(r.GetInt64(3)),
                    Aciertos1X2     = Convert.ToInt32(r.GetInt64(4)),
                    AciertosExactos = Convert.ToInt32(r.GetInt64(5))
                });
            return lista;
        }

        public List<MiPrediccionViewModel> GetMisPredicciones(string autor)
        {
            const string sql = @"
                SELECT 
                    p.partido_id, p.division_id, p.prediccion_1x2,
                    p.goles_local, p.goles_visitante, p.puntos,
                    pa.goleslocal as real_gl, pa.golesvisitante as real_gv, pa.jugado,
                    el.nombre as local_nombre, COALESCE(el.pais_code,'') as local_flag,
                    ev.nombre as visit_nombre, COALESCE(ev.pais_code,'') as visit_flag,
                    f.numero as fecha_num,
                    t.nombre as temporada_nombre
                FROM predicciones p
                JOIN partidos pa ON p.partido_id = pa.id
                JOIN equipos el ON pa.equipolocalid = el.id
                JOIN equipos ev ON pa.equipovisitanteid = ev.id
                JOIN fechas f ON pa.fechaid = f.id
                LEFT JOIN temporadas t ON f.temporada_id = t.id
                WHERE LOWER(TRIM(p.autor)) = LOWER(TRIM(@A))
                ORDER BY t.numero DESC NULLS LAST, f.numero DESC, pa.id";
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@A", autor.Trim());
            conn.Open();
            using var r = cmd.ExecuteReader();
            var lista = new List<MiPrediccionViewModel>();
            while (r.Read())
            {
                int? predGL = r.IsDBNull(3) ? null : r.GetInt32(3);
                int? predGV = r.IsDBNull(4) ? null : r.GetInt32(4);
                int? realGL = r.IsDBNull(6) ? null : r.GetInt32(6);
                int? realGV = r.IsDBNull(7) ? null : r.GetInt32(7);
                bool jugado = r.GetBoolean(8);
                string pred1x2 = r.GetString(2);

                bool? acerto1x2 = null;
                bool? acertoExacto = null;
                if (jugado && realGL.HasValue && realGV.HasValue)
                {
                    string real1x2 = realGL > realGV ? "L" : "V";
                    acerto1x2 = pred1x2 == real1x2;
                    acertoExacto = predGL == realGL && predGV == realGV;
                }

                lista.Add(new MiPrediccionViewModel
                {
                    PartidoId     = r.GetInt32(0),
                    DivisionId    = r.GetInt32(1),
                    Prediccion1x2 = pred1x2,
                    GolesLocalPred = predGL,
                    GolesVisitantePred = predGV,
                    Puntos        = r.IsDBNull(5) ? 0 : r.GetInt32(5),
                    GolesLocalReal = realGL,
                    GolesVisitanteReal = realGV,
                    Jugado        = jugado,
                    NombreLocal   = r.GetString(9),
                    FlagLocal     = r.GetString(10),
                    NombreVisitante = r.GetString(11),
                    FlagVisitante = r.GetString(12),
                    FechaNumero   = r.GetInt32(13),
                    TemporadaNombre = r.IsDBNull(14) ? "" : r.GetString(14),
                    Acerto1x2     = acerto1x2,
                    AcertoExacto  = acertoExacto
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
