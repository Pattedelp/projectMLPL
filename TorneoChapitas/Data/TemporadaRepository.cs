using System.Data;
using Npgsql;
using TorneoAmigos.Models;

namespace TorneoAmigos.Data
{
    public class TemporadaRepository
    {
        private readonly string _connectionString;
        public TemporadaRepository(IConfiguration cfg) =>
            _connectionString = cfg.GetConnectionString("TorneoAmigosDB")
                ?? throw new InvalidOperationException("Connection string not found.");

        private NpgsqlConnection GetConnection() => new(_connectionString);

        // ── TEMPORADAS ──────────────────────────────────

        public Temporada? GetTemporadaActiva()
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                "SELECT id, numero, nombre, fecha_inicio, fecha_fin, activa, finalizada, cant_descensos, cant_ascensos, tiene_promocion, pos_promocion_primera, pos_promocion_b FROM temporadas WHERE activa = true LIMIT 1", conn);
            conn.Open();
            using var r = cmd.ExecuteReader();
            return r.Read() ? MapTemporada(r) : null;
        }

        public List<Temporada> GetTodasLasTemporadas()
        {
            var lista = new List<Temporada>();
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                "SELECT id, numero, nombre, fecha_inicio, fecha_fin, activa, finalizada, cant_descensos, cant_ascensos, tiene_promocion, pos_promocion_primera, pos_promocion_b FROM temporadas ORDER BY numero DESC", conn);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(MapTemporada(r));
            return lista;
        }

        public List<TemporadaResultado> GetResultadosTemporada(int temporadaId)
        {
            var lista = new List<TemporadaResultado>();
            var sql = @"
                SELECT tr.id, tr.temporada_id, tr.equipo_id, e.nombre, tr.division_id, d.nombre,
                       tr.posicion, tr.puntos, tr.partidos_jugados, tr.ganados, tr.perdidos,
                       tr.goles_favor, tr.goles_contra, tr.campeon, tr.ascendio, tr.descendio
                FROM temporada_resultados tr
                INNER JOIN equipos e ON tr.equipo_id = e.id
                INNER JOIN divisiones d ON tr.division_id = d.id
                WHERE tr.temporada_id = @T
                ORDER BY tr.division_id, tr.posicion";

            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@T", temporadaId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(new TemporadaResultado
            {
                Id = r.GetInt32(0), TemporadaId = r.GetInt32(1),
                EquipoId = r.GetInt32(2), NombreEquipo = r.GetString(3),
                FlagCode = BanderaMap.GetCode(r.GetString(3)),
                DivisionId = r.GetInt32(4), NombreDivision = r.GetString(5),
                Posicion = r.GetInt32(6), Puntos = r.GetInt32(7),
                PartidosJugados = r.GetInt32(8), Ganados = r.GetInt32(9),
                Perdidos = r.GetInt32(10), GolesFavor = r.GetInt32(11),
                GolesContra = r.GetInt32(12), Campeon = r.GetBoolean(13),
                Ascendio = r.GetBoolean(14), Descendio = r.GetBoolean(15)
            });
            return lista;
        }

        // ── FINALIZAR TEMPORADA ─────────────────────────

        // ── BORRADOR DE CIERRE DE TEMPORADA ─────────────
        public TemporadaCierre? GetCierre(int temporadaId)
        {
            const string sql = @"
                SELECT tc.id, tc.temporada_id,
                       tc.campeon_copa_id,       ec.nombre,
                       tc.campeon_supercopa_id,  es.nombre,
                       tc.ascenso_1_id,          ea1.nombre,
                       tc.ascenso_2_id,          ea2.nombre,
                       tc.descenso_1_id,         ed1.nombre,
                       tc.descenso_2_id,         ed2.nombre,
                       tc.sin_descensos,
                       tc.campeon_primera_id,    ep.nombre,
                       tc.campeon_b_id,          eb.nombre,
                       tc.ascenso_3_id,          ea3.nombre,
                       tc.descenso_promo_id,     edp.nombre,
                       tc.campeon_c_id,           ecc.nombre,
                       tc.ascenso_c1_id,          ec1.nombre,
                       tc.ascenso_c2_id,          ec2.nombre,
                       tc.descenso_b1_id,         edb1.nombre,
                       tc.descenso_b2_id,         edb2.nombre
                FROM temporada_cierre tc
                LEFT JOIN equipos ec  ON tc.campeon_copa_id      = ec.id
                LEFT JOIN equipos es  ON tc.campeon_supercopa_id = es.id
                LEFT JOIN equipos ea1 ON tc.ascenso_1_id         = ea1.id
                LEFT JOIN equipos ea2 ON tc.ascenso_2_id         = ea2.id
                LEFT JOIN equipos ed1 ON tc.descenso_1_id        = ed1.id
                LEFT JOIN equipos ed2 ON tc.descenso_2_id        = ed2.id
                LEFT JOIN equipos ep  ON tc.campeon_primera_id   = ep.id
                LEFT JOIN equipos eb  ON tc.campeon_b_id         = eb.id
                LEFT JOIN equipos ea3 ON tc.ascenso_3_id         = ea3.id
                LEFT JOIN equipos edp ON tc.descenso_promo_id    = edp.id
                LEFT JOIN equipos ecc ON tc.campeon_c_id          = ecc.id
                LEFT JOIN equipos ec1 ON tc.ascenso_c1_id         = ec1.id
                LEFT JOIN equipos ec2 ON tc.ascenso_c2_id         = ec2.id
                LEFT JOIN equipos edb1 ON tc.descenso_b1_id       = edb1.id
                LEFT JOIN equipos edb2 ON tc.descenso_b2_id       = edb2.id
                WHERE tc.temporada_id = @T";
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@T", temporadaId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new TemporadaCierre
            {
                Id                     = r.GetInt32(0),
                TemporadaId            = r.GetInt32(1),
                CampeonCopaId          = r.IsDBNull(2)  ? null : r.GetInt32(2),
                CampeonCopaNombre      = r.IsDBNull(3)  ? null : r.GetString(3),
                CampeonSupercopaId     = r.IsDBNull(4)  ? null : r.GetInt32(4),
                CampeonSupercoPaNombre = r.IsDBNull(5)  ? null : r.GetString(5),
                Ascenso1Id             = r.IsDBNull(6)  ? null : r.GetInt32(6),
                Ascenso1Nombre         = r.IsDBNull(7)  ? null : r.GetString(7),
                Ascenso2Id             = r.IsDBNull(8)  ? null : r.GetInt32(8),
                Ascenso2Nombre         = r.IsDBNull(9)  ? null : r.GetString(9),
                Descenso1Id            = r.IsDBNull(10) ? null : r.GetInt32(10),
                Descenso1Nombre        = r.IsDBNull(11) ? null : r.GetString(11),
                Descenso2Id            = r.IsDBNull(12) ? null : r.GetInt32(12),
                Descenso2Nombre        = r.IsDBNull(13) ? null : r.GetString(13),
                SinDescensos           = r.GetBoolean(14),
                CampeonPrimeraId       = r.IsDBNull(15) ? null : r.GetInt32(15),
                CampeonBId             = r.IsDBNull(17) ? null : r.GetInt32(17),
                Ascenso3Id             = r.IsDBNull(19) ? null : r.GetInt32(19),
                Ascenso3Nombre         = r.IsDBNull(20) ? null : r.GetString(20),
                DescensoPromoId        = r.IsDBNull(21) ? null : r.GetInt32(21),
                DescensoPromoNombre    = r.IsDBNull(22) ? null : r.GetString(22),
                CampeonCId             = r.IsDBNull(23) ? null : r.GetInt32(23),
                CampeonCNombre         = r.IsDBNull(24) ? null : r.GetString(24),
                AscensoCId1            = r.IsDBNull(25) ? null : r.GetInt32(25),
                AscensoCNombre1        = r.IsDBNull(26) ? null : r.GetString(26),
                AscensoCId2            = r.IsDBNull(27) ? null : r.GetInt32(27),
                AscensoCNombre2        = r.IsDBNull(28) ? null : r.GetString(28),
                DescensoB1Id           = r.IsDBNull(29) ? null : r.GetInt32(29),
                DescensoB1Nombre       = r.IsDBNull(30) ? null : r.GetString(30),
                DescensoB2Id           = r.IsDBNull(31) ? null : r.GetInt32(31),
                DescensoB2Nombre       = r.IsDBNull(32) ? null : r.GetString(32)
            };
        }

        public bool ActualizarConfigTemporada(int temporadaId, int cantDescensos, int cantAscensos,
            bool tienePromocion, int? posPromocionPrimera, int? posPromocionB)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(@"
                UPDATE temporadas SET
                    cant_descensos = @CD, cant_ascensos = @CA, tiene_promocion = @TP,
                    pos_promocion_primera = @PP, pos_promocion_b = @PB
                WHERE id = @Id", conn);
            cmd.Parameters.AddWithValue("@CD", cantDescensos);
            cmd.Parameters.AddWithValue("@CA", cantAscensos);
            cmd.Parameters.AddWithValue("@TP", tienePromocion);
            cmd.Parameters.AddWithValue("@PP", (object?)posPromocionPrimera ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PB", (object?)posPromocionB       ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Id", temporadaId);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        public int GetMaxNumeroTemporadaLegacy()
        {
            // Extrae el número más alto de temporada_nombre en enfrentamientos_historicos
            // ej: "Temporada 16" → 16
            try
            {
                using var conn = GetConnection();
                using var cmd  = new NpgsqlCommand(@"
                    SELECT COALESCE(MAX(CAST(REGEXP_REPLACE(temporada_nombre, '[^0-9]', '', 'g') AS INT)), 0)
                    FROM enfrentamientos_historicos
                    WHERE temporada_nombre ~ '^\s*Temporada\s+\d+'", conn);
                conn.Open();
                return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
            }
            catch { return 0; }
        }

        public bool GenerarFinalOPromocion(int localId, int visitanteId, string tipo)
        {
            using var conn = GetConnection();
            conn.Open();
            int fechaId;
            using (var cmd = new NpgsqlCommand(
                "SELECT id FROM fechas WHERE divisionid = 2 ORDER BY numero DESC LIMIT 1", conn))
            {
                var r = cmd.ExecuteScalar();
                if (r == null) return false;
                fechaId = Convert.ToInt32(r);
            }
            using var ins = new NpgsqlCommand(@"
                INSERT INTO partidos (fechaid, divisionid, equipolocalid, equipovisitanteid, jugado, tipo_partido)
                VALUES (@F, 2, @L, @V, false, @T)", conn);
            ins.Parameters.AddWithValue("@F", fechaId);
            ins.Parameters.AddWithValue("@L", localId);
            ins.Parameters.AddWithValue("@V", visitanteId);
            ins.Parameters.AddWithValue("@T", tipo);
            return ins.ExecuteNonQuery() > 0;
        }

        public bool TodosLosPartidosRegularesJugados(int divisionId)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT COUNT(*) FROM partidos 
                WHERE divisionid = @D AND jugado = false 
                AND COALESCE(tipo_partido,'regular') = 'regular'", conn);
            cmd.Parameters.AddWithValue("@D", divisionId);
            conn.Open();
            return Convert.ToInt32(cmd.ExecuteScalar()) == 0;
        }

        public (bool ok, string msg) GenerarReducido(List<PosicionViewModel> tablaB)
        {
            // Necesitamos al menos 6 equipos
            if (tablaB.Count < 6)
                return (false, "Se necesitan al menos 6 equipos en la tabla para el reducido.");

            // Verificar que no existan ya partidos del reducido
            using var conn = GetConnection();
            conn.Open();
            using (var chk = new NpgsqlCommand(
                "SELECT COUNT(*) FROM partidos WHERE tipo_partido IN ('reducido_semi','reducido_final','promocion') AND divisionid = 2", conn))
            {
                if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                    return (false, "Ya existe un reducido generado para esta temporada.");
            }

            // Obtener la última fecha de la B para agregar los partidos después
            int fechaId;
            using (var cmdF = new NpgsqlCommand(
                "SELECT id FROM fechas WHERE divisionid = 2 ORDER BY numero DESC LIMIT 1", conn))
            {
                var result = cmdF.ExecuteScalar();
                if (result == null) return (false, "No hay fechas en la Primera Nacional.");
                fechaId = Convert.ToInt32(result);
            }

            // 3° vs 6° y 4° vs 5°
            var eq3 = tablaB[2]; var eq6 = tablaB[5];
            var eq4 = tablaB[3]; var eq5 = tablaB[4];

            using var tx = conn.BeginTransaction();
            void InsertarPartidoReducido(int localId, int visitanteId, string tipo)
            {
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO partidos (fechaid, divisionid, equipolocalid, equipovisitanteid, jugado, tipo_partido)
                    VALUES (@F, 2, @L, @V, false, @T)", conn, tx);
                cmd.Parameters.AddWithValue("@F", fechaId);
                cmd.Parameters.AddWithValue("@L", localId);
                cmd.Parameters.AddWithValue("@V", visitanteId);
                cmd.Parameters.AddWithValue("@T", tipo);
                cmd.ExecuteNonQuery();
            }

            InsertarPartidoReducido(eq3.EquipoId, eq6.EquipoId, "reducido_semi");
            InsertarPartidoReducido(eq4.EquipoId, eq5.EquipoId, "reducido_semi");
            // La final y el partido de promoción se agregan después cuando se sepa quiénes pasan

            tx.Commit();
            return (true, "Semifinales del reducido generadas: 3° vs 6° y 4° vs 5°");
        }

        public bool GuardarCierre(TemporadaCierre cierre)
        {
            const string sql = @"
                INSERT INTO temporada_cierre
                    (temporada_id, campeon_copa_id, campeon_supercopa_id,
                     campeon_primera_id, campeon_b_id,
                     ascenso_1_id, ascenso_2_id, ascenso_3_id,
                     descenso_1_id, descenso_2_id, descenso_promo_id,
                     campeon_c_id, ascenso_c1_id, ascenso_c2_id,
                     descenso_b1_id, descenso_b2_id,
                     sin_descensos, updated_at)
                VALUES (@T, @CC, @CS, @CP, @CB, @A1, @A2, @A3, @D1, @D2, @DP, @Cc, @Ac1, @Ac2, @Db1, @Db2, @SD, NOW())
                ON CONFLICT (temporada_id) DO UPDATE SET
                    campeon_copa_id      = EXCLUDED.campeon_copa_id,
                    campeon_supercopa_id = EXCLUDED.campeon_supercopa_id,
                    campeon_primera_id   = EXCLUDED.campeon_primera_id,
                    campeon_b_id         = EXCLUDED.campeon_b_id,
                    ascenso_1_id         = EXCLUDED.ascenso_1_id,
                    ascenso_2_id         = EXCLUDED.ascenso_2_id,
                    ascenso_3_id         = EXCLUDED.ascenso_3_id,
                    descenso_1_id        = EXCLUDED.descenso_1_id,
                    descenso_2_id        = EXCLUDED.descenso_2_id,
                    descenso_promo_id    = EXCLUDED.descenso_promo_id,
                    campeon_c_id         = EXCLUDED.campeon_c_id,
                    ascenso_c1_id        = EXCLUDED.ascenso_c1_id,
                    ascenso_c2_id        = EXCLUDED.ascenso_c2_id,
                    descenso_b1_id       = EXCLUDED.descenso_b1_id,
                    descenso_b2_id       = EXCLUDED.descenso_b2_id,
                    sin_descensos        = EXCLUDED.sin_descensos,
                    updated_at           = NOW()";
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@T",  cierre.TemporadaId);
            cmd.Parameters.AddWithValue("@CC", (object?)cierre.CampeonCopaId      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CS", (object?)cierre.CampeonSupercopaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CP", (object?)cierre.CampeonPrimeraId   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CB", (object?)cierre.CampeonBId         ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@A1", (object?)cierre.Ascenso1Id         ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@A2", (object?)cierre.Ascenso2Id         ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@A3", (object?)cierre.Ascenso3Id         ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@D1", (object?)cierre.Descenso1Id        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@D2", (object?)cierre.Descenso2Id        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DP",  (object?)cierre.DescensoPromoId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Cc",  (object?)cierre.CampeonCId       ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ac1", (object?)cierre.AscensoCId1      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ac2", (object?)cierre.AscensoCId2      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Db1", (object?)cierre.DescensoB1Id      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Db2", (object?)cierre.DescensoB2Id      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SD",  cierre.SinDescensos);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool FinalizarTemporada(int temporadaId, List<PosicionViewModel> tablaPrimera, List<PosicionViewModel> tablaB, bool sinDescensos = false, TemporadaCierre? cierre = null)
        {
            using var conn = GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                // Obtener el nombre de la temporada para el historial
                string temporadaNombre;
                using (var cmdNom = new NpgsqlCommand("SELECT nombre FROM temporadas WHERE id = @Id", conn, tx))
                {
                    cmdNom.Parameters.AddWithValue("@Id", temporadaId);
                    temporadaNombre = (cmdNom.ExecuteScalar() as string) ?? $"Temporada {temporadaId}";
                }

                // ── MIGRAR PARTIDOS JUGADOS A enfrentamientos_historicos ──
                // Esto preserva el historial partido por partido para el Mano a Mano y Tabla Histórica
                using (var cmdMigrar = new NpgsqlCommand(@"
                    INSERT INTO enfrentamientos_historicos
                        (equipo_local_id, equipo_visitante_id, goles_local, goles_visitante, torneo, temporada_nombre, division_id, fecha_numero)
                    SELECT p.equipolocalid, p.equipovisitanteid, p.goleslocal, p.golesvisitante,
                           CASE COALESCE(p.tipo_partido,'regular')
                               WHEN 'reducido_semi'  THEN 'Reducido (Semifinal)'
                               WHEN 'reducido_final' THEN 'Reducido (Final)'
                               WHEN 'promocion'      THEN 'Promoción'
                               ELSE 'Liga'
                           END,
                           @TNom, p.divisionid, f.numero
                    FROM partidos p
                    JOIN fechas f ON p.fechaid = f.id
                    WHERE p.jugado = true
                      AND COALESCE(p.tipo_partido, 'regular') IN ('regular','reducido_semi','reducido_final','promocion')
                      AND NOT EXISTS (
                          SELECT 1 FROM enfrentamientos_historicos eh
                          WHERE eh.equipo_local_id = p.equipolocalid
                            AND eh.equipo_visitante_id = p.equipovisitanteid
                            AND eh.temporada_nombre = @TNom
                            AND eh.goles_local = p.goleslocal
                            AND eh.goles_visitante = p.golesvisitante
                      )", conn, tx))
                {
                    cmdMigrar.Parameters.AddWithValue("@TNom", temporadaNombre);
                    cmdMigrar.ExecuteNonQuery();
                }

                // ── MIGRAR PARTIDOS DE COPA Y SUPERCOPA (de esta temporada) ──
                using (var cmdMigrarCopas = new NpgsqlCommand(@"
                    INSERT INTO enfrentamientos_historicos
                        (equipo_local_id, equipo_visitante_id, goles_local, goles_visitante, torneo, temporada_nombre, division_id)
                    SELECT cp.equipo_local_id, cp.equipo_visitante_id, cp.goles_local, cp.goles_visitante,
                           CASE c.tipo WHEN 'copa_argentina' THEN 'Copa Argentina' ELSE 'Supercopa Argentina' END,
                           @TNom, 0
                    FROM copa_partidos cp
                    JOIN copas c ON cp.copa_id = c.id
                    WHERE c.temporada_id = @T
                      AND cp.jugado = true
                      AND cp.equipo_local_id IS NOT NULL AND cp.equipo_visitante_id IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM enfrentamientos_historicos eh
                          WHERE eh.equipo_local_id = cp.equipo_local_id
                            AND eh.equipo_visitante_id = cp.equipo_visitante_id
                            AND eh.temporada_nombre = @TNom
                            AND eh.goles_local = cp.goles_local
                            AND eh.goles_visitante = cp.goles_visitante
                      )", conn, tx))
                {
                    cmdMigrarCopas.Parameters.AddWithValue("@T", temporadaId);
                    cmdMigrarCopas.Parameters.AddWithValue("@TNom", temporadaNombre);
                    cmdMigrarCopas.ExecuteNonQuery();
                }

                // Guardar resultados Primera División
                GuardarResultados(conn, tx, temporadaId, 1, tablaPrimera);
                // Guardar resultados Primera Nacional
                GuardarResultados(conn, tx, temporadaId, 2, tablaB);
                // Marcar temporada como finalizada
                using var cmd = new NpgsqlCommand(
                    "UPDATE temporadas SET finalizada = true, activa = false, fecha_fin = NOW() WHERE id = @Id", conn, tx);
                cmd.Parameters.AddWithValue("@Id", temporadaId);
                cmd.ExecuteNonQuery();

                // Marcar copas de esta temporada como finalizadas
                using var cmdCopas = new NpgsqlCommand(
                    "UPDATE copas SET finalizada = true WHERE temporada_id = @Id AND finalizada = false", conn, tx);
                cmdCopas.Parameters.AddWithValue("@Id", temporadaId);
                cmdCopas.ExecuteNonQuery();

                tx.Commit();

                // Mover equipos entre divisiones
                // Ascensos: usar borrador si existe, si no usar los 2 primeros de tabla B
                var ascenso1 = cierre?.Ascenso1Id ?? (tablaB.Count >= 1 ? tablaB[0].EquipoId : 0);
                var ascenso2 = cierre?.Ascenso2Id ?? (tablaB.Count >= 2 ? tablaB[1].EquipoId : 0);
                var ascenso3 = cierre?.Ascenso3Id; // solo si ganó el de la B en promoción
                if (ascenso1 > 0) EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 1 WHERE id = @Id", ascenso1);
                if (ascenso2 > 0) EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 1 WHERE id = @Id", ascenso2);
                if (ascenso3.HasValue && ascenso3 > 0) EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 1 WHERE id = @Id", ascenso3.Value);

                // Descensos directos
                if (!sinDescensos)
                {
                    var descenso1 = cierre?.Descenso1Id ?? (tablaPrimera.Count >= 2 ? tablaPrimera[tablaPrimera.Count - 2].EquipoId : 0);
                    var descenso2 = cierre?.Descenso2Id ?? (tablaPrimera.Count >= 1 ? tablaPrimera[tablaPrimera.Count - 1].EquipoId : 0);
                    if (descenso1 > 0) EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 2 WHERE id = @Id", descenso1);
                    if (descenso2 > 0) EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 2 WHERE id = @Id", descenso2);
                }

                // Descenso por promoción (si gana el de la B, el de Primera baja)
                if (cierre?.DescensoPromoId.HasValue == true && cierre.DescensoPromoId > 0)
                {
                    EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 2 WHERE id = @Id", cierre.DescensoPromoId.Value);
                    // Marcar descendio en temporada_resultados
                    using var cmdDp = new NpgsqlCommand(
                        "UPDATE temporada_resultados SET descendio = true WHERE temporada_id = @T AND equipo_id = @E", conn, tx);
                    cmdDp.Parameters.AddWithValue("@T", temporadaId);
                    cmdDp.Parameters.AddWithValue("@E", cierre.DescensoPromoId.Value);
                    cmdDp.ExecuteNonQuery();
                }

                // Ascenso 3 por promoción
                if (cierre?.Ascenso3Id.HasValue == true && cierre.Ascenso3Id > 0)
                {
                    // Marcar ascendio en temporada_resultados
                    using var cmdA3 = new NpgsqlCommand(
                        "UPDATE temporada_resultados SET ascendio = true WHERE temporada_id = @T AND equipo_id = @E", conn, tx);
                    cmdA3.Parameters.AddWithValue("@T", temporadaId);
                    cmdA3.Parameters.AddWithValue("@E", cierre.Ascenso3Id.Value);
                    cmdA3.ExecuteNonQuery();
                }

                // Descensos B → Primera C
                if (PrimeraCActiva())
                {
                    var tablaB2 = new List<PosicionViewModel>();
                    using (var cmdB = new NpgsqlCommand(@"
                        SELECT e.id, COUNT(*) FILTER (WHERE p.jugado AND (
                               (p.equipolocalid=e.id AND p.goleslocal>p.golesvisitante) OR
                               (p.equipovisitanteid=e.id AND p.golesvisitante>p.goleslocal))) as v
                        FROM equipos e
                        LEFT JOIN partidos p ON (p.equipolocalid=e.id OR p.equipovisitanteid=e.id) AND p.divisionid=2
                        WHERE e.divisionid=2 AND e.activo=true
                        GROUP BY e.id ORDER BY v ASC", conn, tx))
                    {
                        using var rB = cmdB.ExecuteReader();
                        while (rB.Read()) tablaB2.Add(new PosicionViewModel { EquipoId = rB.GetInt32(0) });
                    }
                    var db1 = cierre?.DescensoB1Id ?? (tablaB2.Count >= 2 ? tablaB2[tablaB2.Count - 2].EquipoId : 0);
                    var db2 = cierre?.DescensoB2Id ?? (tablaB2.Count >= 1 ? tablaB2[tablaB2.Count - 1].EquipoId : 0);
                    if (db1 > 0)
                    {
                        EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 3 WHERE id = @Id", db1);
                        using var cmdDb1 = new NpgsqlCommand("UPDATE temporada_resultados SET descendio = true WHERE temporada_id = @T AND equipo_id = @E", conn, tx);
                        cmdDb1.Parameters.AddWithValue("@T", temporadaId); cmdDb1.Parameters.AddWithValue("@E", db1); cmdDb1.ExecuteNonQuery();
                    }
                    if (db2 > 0)
                    {
                        EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 3 WHERE id = @Id", db2);
                        using var cmdDb2 = new NpgsqlCommand("UPDATE temporada_resultados SET descendio = true WHERE temporada_id = @T AND equipo_id = @E", conn, tx);
                        cmdDb2.Parameters.AddWithValue("@T", temporadaId); cmdDb2.Parameters.AddWithValue("@E", db2); cmdDb2.ExecuteNonQuery();
                    }
                }

                // Primera C: ascensos a Primera Nacional (si Primera C está activa)
                if (PrimeraCActiva())
                {
                    // Calcular tabla de Primera C directamente con la conexión existente
                    var tablaC = new List<PosicionViewModel>();
                    using (var cmdTC = new NpgsqlCommand(@"
                        SELECT e.id, e.nombre, COALESCE(e.pais_code,''),
                               COUNT(*) FILTER (WHERE p.jugado) as pj,
                               COUNT(*) FILTER (WHERE p.jugado AND (
                                   (p.equipolocalid=e.id AND p.goleslocal>p.golesvisitante) OR
                                   (p.equipovisitanteid=e.id AND p.golesvisitante>p.goleslocal)
                               )) as v,
                               COALESCE(SUM(CASE WHEN p.jugado AND p.equipolocalid=e.id THEN p.goleslocal
                                               WHEN p.jugado AND p.equipovisitanteid=e.id THEN p.golesvisitante ELSE 0 END),0) as gf,
                               COALESCE(SUM(CASE WHEN p.jugado AND p.equipolocalid=e.id THEN p.golesvisitante
                                               WHEN p.jugado AND p.equipovisitanteid=e.id THEN p.goleslocal ELSE 0 END),0) as gc
                        FROM equipos e
                        LEFT JOIN partidos p ON (p.equipolocalid=e.id OR p.equipovisitanteid=e.id)
                            AND p.divisionid=3
                        WHERE e.divisionid=3 AND e.activo=true
                        GROUP BY e.id, e.nombre, e.pais_code
                        ORDER BY v DESC, (gf-gc) DESC", conn, tx))
                    {
                        using var rtc = cmdTC.ExecuteReader();
                        int posC = 1;
                        while (rtc.Read())
                            tablaC.Add(new PosicionViewModel { Posicion = posC++, EquipoId = rtc.GetInt32(0), NombreEquipo = rtc.GetString(1) });
                    }

                    var ascC1 = cierre?.AscensoCId1 ?? (tablaC.Count >= 1 ? tablaC[0].EquipoId : 0);
                    var ascC2 = cierre?.AscensoCId2 ?? (tablaC.Count >= 2 ? tablaC[1].EquipoId : 0);
                    if (ascC1 > 0) EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 2 WHERE id = @Id", ascC1);
                    if (ascC2 > 0) EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 2 WHERE id = @Id", ascC2);
                    var campeonCId = cierre?.CampeonCId ?? (tablaC.Any() ? tablaC.First().EquipoId : 0);
                    if (campeonCId > 0)
                        AgregarTitulo(campeonCId, "campeon_primera_c", "Campeón Primera C Nacional", temporadaId, temporadaNombre);
                }

                // Registrar títulos en el palmarés (temporadaNombre ya fue obtenido arriba)

                // Campeón Primera División
                var campeonPrimId = cierre?.CampeonPrimeraId ?? (tablaPrimera.Any() ? tablaPrimera.First().EquipoId : 0);
                if (campeonPrimId > 0)
                    AgregarTitulo(campeonPrimId, "campeon_torneo", "Campeón Primera División", temporadaId, temporadaNombre);

                // Campeón de la B
                var campeonBId = cierre?.CampeonBId ?? (tablaB.Any() ? tablaB.First().EquipoId : 0);
                if (campeonBId > 0)
                    AgregarTitulo(campeonBId, "campeon_primera_b", "Campeón Primera Nacional", temporadaId, temporadaNombre);

                // Campeón Copa Argentina
                if (cierre?.CampeonCopaId.HasValue == true && cierre.CampeonCopaId > 0)
                    AgregarTitulo(cierre.CampeonCopaId.Value, "campeon_copa", "Campeón Copa Argentina", temporadaId, temporadaNombre);

                // Campeón Supercopa
                if (cierre?.CampeonSupercopaId.HasValue == true && cierre.CampeonSupercopaId > 0)
                    AgregarTitulo(cierre.CampeonSupercopaId.Value, "campeon_supercopa", "Campeón Supercopa Argentina", temporadaId, temporadaNombre);

                return true;
            }
            catch { tx.Rollback(); return false; }
        }

        private void GuardarResultados(NpgsqlConnection conn, NpgsqlTransaction tx,
            int temporadaId, int divisionId, List<PosicionViewModel> tabla, bool sinDescensos = false)
        {
            int totalEquipos = tabla.Count;
            for (int i = 0; i < tabla.Count; i++)
            {
                var f = tabla[i];
                bool campeon   = divisionId == 1 && i == 0;
                bool descendio = (!sinDescensos && divisionId == 1 && i >= totalEquipos - 2)
                              || (divisionId == 2 && PrimeraCActiva() && i >= totalEquipos - 2);
                bool ascendio  = divisionId == 2 && i < 2;

                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO temporada_resultados
                        (temporada_id, equipo_id, division_id, posicion, puntos,
                         partidos_jugados, ganados, perdidos, goles_favor, goles_contra,
                         campeon, ascendio, descendio)
                    VALUES (@T, @E, @D, @Pos, @Pts, @PJ, @G, @P, @GF, @GC, @Camp, @Asc, @Des)", conn, tx);
                cmd.Parameters.AddWithValue("@T",    temporadaId);
                cmd.Parameters.AddWithValue("@E",    f.EquipoId);
                cmd.Parameters.AddWithValue("@D",    divisionId);
                cmd.Parameters.AddWithValue("@Pos",  f.Posicion);
                cmd.Parameters.AddWithValue("@Pts",  f.Puntos);
                cmd.Parameters.AddWithValue("@PJ",   f.PartidosJugados);
                cmd.Parameters.AddWithValue("@G",    f.Ganados);
                cmd.Parameters.AddWithValue("@P",    f.Perdidos);
                cmd.Parameters.AddWithValue("@GF",   f.GolesAFavor);
                cmd.Parameters.AddWithValue("@GC",   f.GolesEnContra);
                cmd.Parameters.AddWithValue("@Camp", campeon);
                cmd.Parameters.AddWithValue("@Asc",  ascendio);
                cmd.Parameters.AddWithValue("@Des",  descendio);
                cmd.ExecuteNonQuery();
            }
        }

        // ── NUEVA TEMPORADA ─────────────────────────────

        public int CrearNuevaTemporada(string nombre, List<int> equiposPrimera, List<int> equiposB,
            List<(string nombre, int divisionId)> equiposNuevos,
            int cantDescensos = 2, int cantAscensos = 2, bool tienePromocion = false,
            int? posPromocionPrimera = null, int? posPromocionB = null,
            List<int>? equiposC = null)
        {
            using var conn = GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                // Obtener numero siguiente
                int numero;
                using (var cmd = new NpgsqlCommand("SELECT COALESCE(MAX(numero), 0) + 1 FROM temporadas", conn, tx))
                    numero = Convert.ToInt32(cmd.ExecuteScalar());

                // Crear temporada
                int tempId;
                using (var cmd = new NpgsqlCommand(
                    "INSERT INTO temporadas (numero, nombre, fecha_inicio, activa, finalizada, cant_descensos, cant_ascensos, tiene_promocion, pos_promocion_primera, pos_promocion_b) VALUES (@N, @Nom, NOW(), true, false, @CD, @CA, @TP, @PP, @PB) RETURNING id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@N",   numero);
                    cmd.Parameters.AddWithValue("@Nom", nombre);
                    cmd.Parameters.AddWithValue("@CD",  cantDescensos);
                    cmd.Parameters.AddWithValue("@CA",  cantAscensos);
                    cmd.Parameters.AddWithValue("@TP",  tienePromocion);
                    cmd.Parameters.AddWithValue("@PP",  (object?)posPromocionPrimera ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@PB",  (object?)posPromocionB       ?? DBNull.Value);
                    tempId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // Agregar equipos nuevos a la BD
                foreach (var eq in equiposNuevos)
                {
                    using var cmd = new NpgsqlCommand(
                        "INSERT INTO equipos (divisionid, nombre, colorprincipal, colorsecundario, activo) VALUES (@D, @N, '#003366', '#FFD700', true) RETURNING id", conn, tx);
                    cmd.Parameters.AddWithValue("@D", eq.divisionId);
                    cmd.Parameters.AddWithValue("@N", eq.nombre);
                    int nuevoId = Convert.ToInt32(cmd.ExecuteScalar());
                    if (eq.divisionId == 1) equiposPrimera.Add(nuevoId);
                    else equiposB.Add(nuevoId);
                }

                // Actualizar divisiones de equipos (ascensos/descensos)
                foreach (var id in equiposPrimera)
                    EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 1, activo = true WHERE id = @Id", id);
                foreach (var id in equiposB)
                    EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 2, activo = true WHERE id = @Id", id);

                // Desactivar equipos que no participan
                EjecutarUpdateLista(conn, tx, equiposPrimera.Concat(equiposB).ToList());

                // Borrar fixture anterior
                BorrarFixture(conn, tx);

                // Generar fixture
                GenerarFixture(conn, tx, 1, equiposPrimera);
                GenerarFixture(conn, tx, 2, equiposB);
                // Primera C: usar lista pasada o buscar los de división 3
                var idsC = (equiposC != null && equiposC.Any())
                    ? equiposC
                    : GetEquiposIdsByDivision(3, conn);
                if (idsC.Any())
                    GenerarFixture(conn, tx, 3, idsC, PrimeraCIdaVuelta());

                tx.Commit();
                return tempId;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                throw new Exception("Error creando temporada: " + ex.Message);
            }
        }

        public bool RegenerarFixture()
        {
            // Solo si no hay partidos jugados
            using var conn = GetConnection();
            conn.Open();
            using var chk = new NpgsqlCommand(
                "SELECT COUNT(*) FROM partidos WHERE jugado = true", conn);
            if (Convert.ToInt32(chk.ExecuteScalar()) > 0)
                return false;

            var equiposPrimera = GetEquiposIdsByDivision(1, conn);
            var equiposB       = GetEquiposIdsByDivision(2, conn);
            var equiposC       = GetEquiposIdsByDivision(3, conn);

            using var tx = conn.BeginTransaction();
            BorrarFixture(conn, tx);
            if (equiposPrimera.Any()) GenerarFixture(conn, tx, 1, equiposPrimera);
            if (equiposB.Any())       GenerarFixture(conn, tx, 2, equiposB);
            if (equiposC.Any())       GenerarFixture(conn, tx, 3, equiposC, PrimeraCIdaVuelta());
            tx.Commit();
            return true;
        }

        private List<int> GetEquiposIdsByDivision(int divisionId, NpgsqlConnection conn)
        {
            var ids = new List<int>();
            using var cmd = new NpgsqlCommand(
                "SELECT id FROM equipos WHERE divisionid = @D AND activo = true ORDER BY id", conn);
            cmd.Parameters.AddWithValue("@D", divisionId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) ids.Add(r.GetInt32(0));
            return ids;
        }

        public bool BorrarFixtureSinResultados(int temporadaId)
        {
            // Solo borra si NO hay ningún partido jugado
            using var conn = GetConnection();
            conn.Open();
            using var check = new NpgsqlCommand(
                "SELECT COUNT(*) FROM partidos p JOIN fechas f ON p.fechaid = f.id WHERE f.divisionid IN (1,2) AND p.jugado = true", conn);
            var jugados = Convert.ToInt32(check.ExecuteScalar());
            if (jugados > 0) return false; // No borrar si hay resultados

            using var tx = conn.BeginTransaction();
            new NpgsqlCommand("DELETE FROM partidos WHERE fechaid IN (SELECT id FROM fechas WHERE divisionid IN (1,2))", conn, tx).ExecuteNonQuery();
            new NpgsqlCommand("DELETE FROM fechas WHERE divisionid IN (1,2)", conn, tx).ExecuteNonQuery();
            tx.Commit();
            return true;
        }

        private void BorrarFixture(NpgsqlConnection conn, NpgsqlTransaction tx)
        {
            new NpgsqlCommand("DELETE FROM partidos", conn, tx).ExecuteNonQuery();
            new NpgsqlCommand("DELETE FROM fechas", conn, tx).ExecuteNonQuery();
        }

        private void GenerarFixture(NpgsqlConnection conn, NpgsqlTransaction tx, int divisionId, List<int> equipos, bool idaYVuelta = false)
        {
            if (equipos.Count < 2) return;

            var lista = new List<int>(equipos);
            if (lista.Count % 2 != 0) lista.Add(-1);
            int n = lista.Count;
            int numFechasIda = n - 1;
            int numFechas = idaYVuelta ? numFechasIda * 2 : numFechasIda;

            var fechaIds = new List<int>();
            for (int f = 1; f <= numFechas; f++)
            {
                int fechaId;
                using var cmd = new NpgsqlCommand(
                    "INSERT INTO fechas (divisionid, numero, nombre, activa, habilitada, temporada_id) VALUES (@D, @N, @Nom, true, false, (SELECT id FROM temporadas WHERE activa = true ORDER BY id DESC FETCH FIRST 1 ROW ONLY)) RETURNING id", conn, tx);
                cmd.Parameters.AddWithValue("@D",   divisionId);
                cmd.Parameters.AddWithValue("@N",   f);
                cmd.Parameters.AddWithValue("@Nom", $"Fecha {f}");
                fechaId = Convert.ToInt32(cmd.ExecuteScalar());
                fechaIds.Add(fechaId);
            }

            using (var cmd = new NpgsqlCommand("UPDATE fechas SET habilitada = true WHERE id = @Id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", fechaIds[0]);
                cmd.ExecuteNonQuery();
            }

            // Guardar partidos de ida para invertirlos en vuelta
            var partidosIda = new List<(int local, int visitante)>();

            var listaIda = new List<int>(lista);
            for (int fecha = 0; fecha < numFechasIda; fecha++)
            {
                int fechaId = fechaIds[fecha];
                for (int i = 0; i < n / 2; i++)
                {
                    int local     = listaIda[i];
                    int visitante = listaIda[n - 1 - i];
                    if (local == -1 || visitante == -1) continue;

                    partidosIda.Add((local, visitante));
                    using var cmd = new NpgsqlCommand(@"
                        INSERT INTO partidos (fechaid, divisionid, equipolocalid, equipovisitanteid, jugado)
                        VALUES (@F, @D, @L, @V, false)", conn, tx);
                    cmd.Parameters.AddWithValue("@F", fechaId);
                    cmd.Parameters.AddWithValue("@D", divisionId);
                    cmd.Parameters.AddWithValue("@L", local);
                    cmd.Parameters.AddWithValue("@V", visitante);
                    cmd.ExecuteNonQuery();
                }
                var ultimo = listaIda[listaIda.Count - 1];
                listaIda.RemoveAt(listaIda.Count - 1);
                listaIda.Insert(1, ultimo);
            }

            // Vuelta: mismos partidos pero con local/visitante invertidos
            if (idaYVuelta)
            {
                int partidoIdx = 0;
                for (int fecha = numFechasIda; fecha < numFechas; fecha++)
                {
                    int fechaId = fechaIds[fecha];
                    int partidosPorFecha = n / 2;
                    for (int i = 0; i < partidosPorFecha && partidoIdx < partidosIda.Count; i++, partidoIdx++)
                    {
                        var (local, visitante) = partidosIda[partidoIdx];
                        using var cmd = new NpgsqlCommand(@"
                            INSERT INTO partidos (fechaid, divisionid, equipolocalid, equipovisitanteid, jugado)
                            VALUES (@F, @D, @L, @V, false)", conn, tx);
                        cmd.Parameters.AddWithValue("@F", fechaId);
                        cmd.Parameters.AddWithValue("@D", divisionId);
                        cmd.Parameters.AddWithValue("@L", visitante); // invertido
                        cmd.Parameters.AddWithValue("@V", local);     // invertido
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void EjecutarUpdate(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, int id)
        {
            using var cmd = new NpgsqlCommand(sql, conn, tx);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        private void EjecutarUpdateLista(NpgsqlConnection conn, NpgsqlTransaction tx, List<int> activos)
        {
            if (!activos.Any()) return;
            var ids = string.Join(",", activos);
            new NpgsqlCommand($"UPDATE equipos SET activo = false WHERE id NOT IN ({ids})", conn, tx).ExecuteNonQuery();
        }

        // ── COPAS ───────────────────────────────────────

        // Partidos de copa pendientes con AMBOS equipos ya definidos (para Predicciones)
        public List<CopaPartido> GetPartidosCopaPendientes(string tipoCopa)
        {
            var lista = new List<CopaPartido>();
            using var conn = GetConnection();
            conn.Open();

            var sql = @"
                SELECT cp.id, cp.ronda_id, cp.copa_id,
                       cp.equipo_local_id, cp.equipo_visitante_id,
                       el.nombre, COALESCE(el.pais_code,''),
                       ev.nombre, COALESCE(ev.pais_code,''),
                       cp.goles_local, cp.goles_visitante, cp.jugado, cp.posicion_bracket
                FROM copa_partidos cp
                JOIN copas c ON cp.copa_id = c.id
                JOIN equipos el ON cp.equipo_local_id = el.id
                JOIN equipos ev ON cp.equipo_visitante_id = ev.id
                WHERE c.tipo = @Tipo AND c.finalizada = false
                  AND cp.equipo_local_id IS NOT NULL AND cp.equipo_visitante_id IS NOT NULL
                  AND cp.jugado = false
                ORDER BY cp.id";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Tipo", tipoCopa);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var nombreLocal = r.GetString(5);
                var nombreVisit = r.GetString(7);
                var paisLocal   = r.GetString(6);
                var paisVisit   = r.GetString(8);

                lista.Add(new CopaPartido
                {
                    Id                = r.GetInt32(0),
                    RondaId           = r.GetInt32(1),
                    CopaId            = r.GetInt32(2),
                    EquipoLocalId     = r.GetInt32(3),
                    EquipoVisitanteId = r.GetInt32(4),
                    NombreLocal       = nombreLocal,
                    FlagLocal         = !string.IsNullOrEmpty(paisLocal) ? paisLocal : BanderaMap.GetCode(nombreLocal),
                    NombreVisitante   = nombreVisit,
                    FlagVisitante     = !string.IsNullOrEmpty(paisVisit) ? paisVisit : BanderaMap.GetCode(nombreVisit),
                    GolesLocal        = r.IsDBNull(9)  ? null : r.GetInt32(9),
                    GolesVisitante    = r.IsDBNull(10) ? null : r.GetInt32(10),
                    Jugado            = r.GetBoolean(11),
                    PosicionBracket   = r.GetInt32(12)
                });
            }
            return lista;
        }

        public int GetDivisionIdParaCopa(string tipoCopa) => tipoCopa switch
        {
            "copa_argentina" => 100,
            "supercopa"      => 101,
            _                => 0
        };

        public Copa? GetCopaActiva(string tipo)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                "SELECT id, temporada_id, tipo, nombre, finalizada FROM copas WHERE tipo = @T AND finalizada = false ORDER BY id DESC LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@T", tipo);
            conn.Open();
            using var r = cmd.ExecuteReader();
            return r.Read() ? MapCopa(r) : null;
        }

        public CopaFullViewModel? GetCopaFull(string tipo)
        {
            var copa = GetCopaActiva(tipo);
            if (copa == null) return null;

            var rondas = GetRondasCopa(copa.Id);
            return new CopaFullViewModel { Copa = copa, Rondas = rondas };
        }

        public List<CopaRonda> GetRondasCopa(int copaId)
        {
            var rondas = new List<CopaRonda>();
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                "SELECT id, copa_id, nombre, orden, habilitada FROM copa_rondas WHERE copa_id = @C ORDER BY orden", conn);
            cmd.Parameters.AddWithValue("@C", copaId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                rondas.Add(new CopaRonda
                {
                    Id = r.GetInt32(0), CopaId = r.GetInt32(1),
                    Nombre = r.GetString(2), Orden = r.GetInt32(3), Habilitada = r.GetBoolean(4)
                });
            }
            r.Close();

            foreach (var ronda in rondas)
                ronda.Partidos = GetPartidosCopa(copaId, ronda.Id, conn);

            return rondas;
        }

        private List<CopaPartido> GetPartidosCopa(int copaId, int rondaId, NpgsqlConnection conn)
        {
            var lista = new List<CopaPartido>();
            var sql = @"
                SELECT cp.id, cp.ronda_id, cp.copa_id,
                       cp.equipo_local_id, COALESCE(el.nombre, 'Por definir'),
                       COALESCE(el.pais_code, '') as flag_local,
                       cp.equipo_visitante_id, COALESCE(ev.nombre, 'Por definir'),
                       COALESCE(ev.pais_code, '') as flag_visitante,
                       cp.goles_local, cp.goles_visitante, cp.jugado, cp.posicion_bracket
                FROM copa_partidos cp
                LEFT JOIN equipos el ON cp.equipo_local_id = el.id
                LEFT JOIN equipos ev ON cp.equipo_visitante_id = ev.id
                WHERE cp.copa_id = @C AND cp.ronda_id = @R
                ORDER BY cp.posicion_bracket";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@C", copaId);
            cmd.Parameters.AddWithValue("@R", rondaId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var nombreLocal     = r.GetString(4);
                var paisLocal       = r.GetString(5);
                var nombreVisitante = r.GetString(7);
                var paisVisitante   = r.GetString(8);

                lista.Add(new CopaPartido
                {
                    Id = r.GetInt32(0), RondaId = r.GetInt32(1), CopaId = r.GetInt32(2),
                    EquipoLocalId     = r.IsDBNull(3) ? null : r.GetInt32(3),
                    NombreLocal       = nombreLocal,
                    FlagLocal         = r.IsDBNull(3) ? "" :
                                        !string.IsNullOrEmpty(paisLocal) ? paisLocal :
                                        BanderaMap.GetCode(nombreLocal),
                    EquipoVisitanteId = r.IsDBNull(6) ? null : r.GetInt32(6),
                    NombreVisitante   = nombreVisitante,
                    FlagVisitante     = r.IsDBNull(6) ? "" :
                                        !string.IsNullOrEmpty(paisVisitante) ? paisVisitante :
                                        BanderaMap.GetCode(nombreVisitante),
                    GolesLocal        = r.IsDBNull(9)  ? null : r.GetInt32(9),
                    GolesVisitante    = r.IsDBNull(10) ? null : r.GetInt32(10),
                    Jugado            = r.GetBoolean(11),
                    PosicionBracket   = r.GetInt32(12)
                });
            }
            return lista;
        }

        public int SortearCopaArgentina(int temporadaId, List<int> equiposPrimera, List<int> equiposB,
            List<int>? equiposNuevosB = null, List<int>? equiposC = null)
        {
            using var conn = GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                // Migrar partidos anteriores al historial
                string temporadaNombreCopa;
                using (var cmdNom = new NpgsqlCommand("SELECT nombre FROM temporadas WHERE id = @Id", conn, tx))
                {
                    cmdNom.Parameters.AddWithValue("@Id", temporadaId);
                    temporadaNombreCopa = (cmdNom.ExecuteScalar() as string) ?? $"Temporada {temporadaId}";
                }

                using (var cmdMigrarCopa = new NpgsqlCommand(@"
                    INSERT INTO enfrentamientos_historicos
                        (equipo_local_id, equipo_visitante_id, goles_local, goles_visitante, torneo, temporada_nombre, division_id)
                    SELECT cp.equipo_local_id, cp.equipo_visitante_id, cp.goles_local, cp.goles_visitante,
                           'Copa Argentina', @TNom, 0
                    FROM copa_partidos cp
                    JOIN copas c ON cp.copa_id = c.id
                    WHERE c.tipo = 'copa_argentina' AND c.temporada_id = @T
                      AND cp.jugado = true
                      AND cp.equipo_local_id IS NOT NULL AND cp.equipo_visitante_id IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM enfrentamientos_historicos eh
                          WHERE eh.equipo_local_id = cp.equipo_local_id
                            AND eh.equipo_visitante_id = cp.equipo_visitante_id
                            AND eh.temporada_nombre = @TNom
                            AND eh.goles_local = cp.goles_local
                            AND eh.goles_visitante = cp.goles_visitante
                      )", conn, tx))
                {
                    cmdMigrarCopa.Parameters.AddWithValue("@T", temporadaId);
                    cmdMigrarCopa.Parameters.AddWithValue("@TNom", temporadaNombreCopa);
                    cmdMigrarCopa.ExecuteNonQuery();
                }

                // Limpiar copa previa
                using (var del = new NpgsqlCommand(@"
                    DELETE FROM copa_partidos WHERE copa_id IN
                        (SELECT id FROM copas WHERE tipo='copa_argentina' AND temporada_id=@T);
                    DELETE FROM copa_rondas WHERE copa_id IN
                        (SELECT id FROM copas WHERE tipo='copa_argentina' AND temporada_id=@T);
                    DELETE FROM copas WHERE tipo='copa_argentina' AND temporada_id=@T;", conn, tx))
                {
                    del.Parameters.AddWithValue("@T", temporadaId);
                    del.ExecuteNonQuery();
                }

                int copaId;
                using (var cmd = new NpgsqlCommand(
                    "INSERT INTO copas (temporada_id, tipo, nombre, finalizada) VALUES (@T, 'copa_argentina', 'Copa Argentina', false) RETURNING id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@T", temporadaId);
                    copaId = Convert.ToInt32(cmd.ExecuteScalar());
                }

// ── LÓGICA DEL SORTEO (GENERAL) ────────────────────────────────
                // Objetivo fijo: Octavos = 16 (8 mejores de Primera + 8 ganadores de FP2).
                // Se arma con máximo 2 fases previas (FP1 y FP2), calculadas para cualquier N.
                var rng = new Random();

                int nP = equiposPrimera.Count;

                // 8 mejores de Primera → Octavos. El resto de Primera baja a previas.
                var P_octavos = equiposPrimera.Take(8).ToList();
                var P_previa  = equiposPrimera.Skip(8).ToList();   // los peores de Primera

                // Lista de "equipos de fase previa" ordenada de PEOR a MEJOR (criterio de siembra):
                //   1° los de C (todos, son los más débiles)
                //   2° los de B de peor a mejor (los últimos de la lista son los peores)
                //   3° los peores de Primera (P_previa)
                // equiposB viene ordenado de MEJOR (índice 0) a PEOR (último) según la tabla.
                var previa = new List<int>();
                if (equiposC != null && equiposC.Any())
                    previa.AddRange(equiposC);                       // C primero (más débiles)
                previa.AddRange(Enumerable.Reverse(equiposB));       // B de peor a mejor
                previa.AddRange(P_previa);                           // peores de Primera al final (más fuertes)

                int resto = previa.Count;   // total de equipos que pasan por fases previas

                // Cantidad de equipos que juegan FP1 = 2 * (resto - 16), acotado a [0, resto].
                // Si resto <= 16, no hay FP1 (todos entran directo a FP2).
                int cantFP1 = Math.Max(0, 2 * (resto - 16));
                if (cantFP1 > resto) cantFP1 = resto;
                if (cantFP1 % 2 != 0) cantFP1--;   // debe ser par

                int ganFP1 = cantFP1 / 2;
                int slotsFP2 = resto - cantFP1 + ganFP1;  // equipos directos a FP2 + ganadores FP1

                // Los `cantFP1` PEORES juegan FP1; el resto entra directo a FP2.
                var equiposFP1     = previa.Take(cantFP1).ToList();          // peores → FP1
                var equiposDirFP2  = previa.Skip(cantFP1).ToList();          // mejores → directo a FP2

                // Mezclar dentro de cada bolsa para que el cruce sea aleatorio
                var fp1Rand    = equiposFP1.OrderBy(_ => rng.Next()).ToList();
                var dirFP2Rand = equiposDirFP2.OrderBy(_ => rng.Next()).ToList();
                var octRand    = P_octavos.OrderBy(_ => rng.Next()).ToList();

                // ── CREAR RONDAS ───────────────────────────────────────────────
                var rondasDef = new List<(string nombre, int orden, bool habilitada)>();
                bool hayFP1 = cantFP1 > 0;
                if (hayFP1) rondasDef.Add(("Fase Previa 1", 1, true));
                rondasDef.Add(("Fase Previa 2",     2, !hayFP1));
                rondasDef.Add(("Octavos de Final",  3, false));
                rondasDef.Add(("Cuartos de Final",  4, false));
                rondasDef.Add(("Semifinales",       5, false));
                rondasDef.Add(("Final",             6, false));

                var rondaIds = new Dictionary<string, int>();
                foreach (var (nombre, orden, habilitada) in rondasDef)
                {
                    using var cmd = new NpgsqlCommand(
                        "INSERT INTO copa_rondas (copa_id, nombre, orden, habilitada) VALUES (@C, @N, @O, @H) RETURNING id", conn, tx);
                    cmd.Parameters.AddWithValue("@C", copaId);
                    cmd.Parameters.AddWithValue("@N", nombre);
                    cmd.Parameters.AddWithValue("@O", orden);
                    cmd.Parameters.AddWithValue("@H", habilitada);
                    rondaIds[nombre] = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // ── FASE PREVIA 1: los peores entre sí (cantFP1/2 partidos) ─────
                if (hayFP1)
                {
                    int pos = 0;
                    for (int i = 0; i + 1 < fp1Rand.Count; i += 2)
                        InsertarPartidoCopa(conn, tx, rondaIds["Fase Previa 1"], copaId, fp1Rand[i], fp1Rand[i + 1], pos++);
                }

                // ── FASE PREVIA 2: 8 partidos ──────────────────────────────────
                // Poblar 16 slots: primero los equipos directos (de a pares),
                // y los últimos `ganFP1` slots quedan esperando al ganador de FP1.
                {
                    int pos = 0;
                    int idx = 0;

                    // Partidos completos entre equipos directos.
                    // Cantidad de partidos "llenos" = (slotsFP2 - ganFP1) / 2  = equipos directos / 2
                    int partidosDirectosLlenos = dirFP2Rand.Count / 2;

                    // Pero algunos de esos partidos directos deben dejar UN slot libre
                    // para el ganador de FP1. Resolvemos así:
                    //   - `ganFP1` partidos tendrán: 1 equipo directo + (slot vacío para ganador FP1)
                    //   - el resto de partidos: 2 equipos directos
                    // Total partidos FP2 = 8 siempre.

                    // 1) Partidos que esperan ganador de FP1 (1 equipo directo cada uno)
                    for (int i = 0; i < ganFP1 && idx < dirFP2Rand.Count; i++)
                        InsertarPartidoCopaConUnEquipo(conn, tx, rondaIds["Fase Previa 2"], copaId, dirFP2Rand[idx++], pos++);

                    // 2) Partidos llenos entre equipos directos restantes
                    while (idx + 1 < dirFP2Rand.Count)
                        InsertarPartidoCopa(conn, tx, rondaIds["Fase Previa 2"], copaId, dirFP2Rand[idx++], dirFP2Rand[idx++], pos++);

                    // 3) Si quedó un equipo directo suelto (caso impar raro), darle un slot solo
                    if (idx < dirFP2Rand.Count)
                        InsertarPartidoCopaConUnEquipo(conn, tx, rondaIds["Fase Previa 2"], copaId, dirFP2Rand[idx++], pos++);
                }

                // ── OCTAVOS: 8 mejores de Primera esperan ganador de FP2 ───────
                for (int i = 0; i < octRand.Count; i++)
                    InsertarPartidoCopaConUnEquipo(conn, tx, rondaIds["Octavos de Final"], copaId, octRand[i], i);

                // ── CUARTOS, SEMIS, FINAL: partidos vacíos ─────────────────────
                for (int i = 0; i < 4; i++)
                    InsertarPartidoCopaVacio(conn, tx, rondaIds["Cuartos de Final"], copaId, i);
                for (int i = 0; i < 2; i++)
                    InsertarPartidoCopaVacio(conn, tx, rondaIds["Semifinales"], copaId, i);
                InsertarPartidoCopaVacio(conn, tx, rondaIds["Final"], copaId, 0);
                
                tx.Commit();
                return copaId;
            }
            catch (Exception ex) { tx.Rollback(); throw new Exception("Error sorteando copa: " + ex.Message); }
        }

                public int SortearSupercopa(int temporadaId, int campeonId, int subcampeonId, int campeonCopaId, bool conSemifinal = false)
        {
            using var conn = GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                int copaId;
                using (var cmd = new NpgsqlCommand(
                    "INSERT INTO copas (temporada_id, tipo, nombre, finalizada) VALUES (@T, 'supercopa', 'Supercopa Argentina', false) RETURNING id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@T", temporadaId);
                    copaId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                if (conSemifinal)
                {
                    // Campeón del torneo == Campeón copa → Semifinal primero
                    int semifinalId;
                    using (var cmd = new NpgsqlCommand(
                        "INSERT INTO copa_rondas (copa_id, nombre, orden, habilitada) VALUES (@C, 'Semifinal', 1, true) RETURNING id", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@C", copaId);
                        semifinalId = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    InsertarPartidoCopa(conn, tx, semifinalId, copaId, campeonId, subcampeonId, 0);

                    int finalId;
                    using (var cmd = new NpgsqlCommand(
                        "INSERT INTO copa_rondas (copa_id, nombre, orden, habilitada) VALUES (@C, 'Final', 2, false) RETURNING id", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@C", copaId);
                        finalId = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    InsertarPartidoCopaVacio(conn, tx, finalId, copaId, 0);
                }
                else
                {
                    // Distinto campeón → Final directa
                    int finalId;
                    using (var cmd = new NpgsqlCommand(
                        "INSERT INTO copa_rondas (copa_id, nombre, orden, habilitada) VALUES (@C, 'Final', 1, true) RETURNING id", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@C", copaId);
                        finalId = Convert.ToInt32(cmd.ExecuteScalar());
                    }
                    InsertarPartidoCopa(conn, tx, finalId, copaId, campeonId, campeonCopaId, 0);
                }

                tx.Commit();
                return copaId;
            }
            catch (Exception ex) { tx.Rollback(); throw new Exception("Error sorteando supercopa: " + ex.Message); }
        }

        public string? GetTipoCopaDePartido(int copaPartidoId)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT c.tipo FROM copa_partidos cp
                JOIN copas c ON cp.copa_id = c.id
                WHERE cp.id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", copaPartidoId);
            conn.Open();
            var result = cmd.ExecuteScalar();
            return result?.ToString();
        }

        public bool GuardarResultadoCopa(int partidoId, int golesLocal, int golesVisitante)
        {
            using var conn = GetConnection();
            conn.Open();
            using var tx = conn.BeginTransaction();
            try
            {
                // Guardar resultado
                using (var cmd = new NpgsqlCommand(
                    "UPDATE copa_partidos SET goles_local=@GL, goles_visitante=@GV, jugado=true WHERE id=@Id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@GL", golesLocal);
                    cmd.Parameters.AddWithValue("@GV", golesVisitante);
                    cmd.Parameters.AddWithValue("@Id", partidoId);
                    cmd.ExecuteNonQuery();
                }

                // Obtener datos del partido
                int copaId, rondaId, posicion, ganadorId;
                string nombreRonda = "";
                using (var cmd = new NpgsqlCommand(@"
                    SELECT cp.copa_id, cp.ronda_id, cp.posicion_bracket,
                           CASE WHEN cp.goles_local > cp.goles_visitante THEN cp.equipo_local_id
                                ELSE cp.equipo_visitante_id END as ganador_id,
                           cr.nombre as ronda_nombre
                    FROM copa_partidos cp
                    JOIN copa_rondas cr ON cp.ronda_id = cr.id
                    WHERE cp.id = @Id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@Id", partidoId);
                    using var r = cmd.ExecuteReader();
                    if (!r.Read()) { tx.Rollback(); return false; }
                    copaId      = r.GetInt32(0);
                    rondaId     = r.GetInt32(1);
                    posicion    = r.GetInt32(2);
                    ganadorId   = r.IsDBNull(3) ? 0 : r.GetInt32(3);
                    nombreRonda = r.GetString(4);
                }

                // AUTO-AVANCE: buscar slot vacío en la siguiente ronda
                if (ganadorId > 0)
                {
                    int? siguienteRondaId = null;
                    string siguienteRondaNombre = "";
                    using (var cmd2 = new NpgsqlCommand(@"
                        SELECT cr.id, cr.nombre FROM copa_rondas cr
                        WHERE cr.copa_id = @C
                          AND cr.orden > (SELECT orden FROM copa_rondas WHERE id = @R)
                        ORDER BY cr.orden FETCH FIRST 1 ROW ONLY", conn, tx))
                    {
                        cmd2.Parameters.AddWithValue("@C", copaId);
                        cmd2.Parameters.AddWithValue("@R", rondaId);
                        using var r2 = cmd2.ExecuteReader();
                        if (r2.Read()) { siguienteRondaId = r2.GetInt32(0); siguienteRondaNombre = r2.GetString(1); }
                    }

                    if (siguienteRondaId.HasValue)
                    {
                        // Para FP1 → FP2: buscar el primer slot completamente vacío (local=NULL)
                        // Para otras rondas: usar posicion/2
                        bool esFP0 = nombreRonda?.Contains("Fase Previa 0") == true;
                        bool esFP1 = nombreRonda?.Contains("Fase Previa 1") == true;

                        if (esFP0 || esFP1)
                        {
                            // Buscar primer partido en siguiente ronda donde local_id IS NULL
                            using var cmdAdv = new NpgsqlCommand(@"
                                UPDATE copa_partidos
                                SET equipo_local_id = @G
                                WHERE id = (
                                    SELECT id FROM copa_partidos
                                    WHERE ronda_id = @SR AND copa_id = @C
                                      AND equipo_local_id IS NULL
                                    ORDER BY posicion_bracket FETCH FIRST 1 ROW ONLY
                                )", conn, tx);
                            cmdAdv.Parameters.AddWithValue("@G",  ganadorId);
                            cmdAdv.Parameters.AddWithValue("@SR", siguienteRondaId.Value);
                            cmdAdv.Parameters.AddWithValue("@C",  copaId);
                            cmdAdv.ExecuteNonQuery();
                        }
                        else
                        {
                            // Contar partidos en ronda actual y siguiente para determinar la relación
                            int partidosRondaActual = 0, partidosRondaSiguiente = 0;
                            using (var cmdCount = new NpgsqlCommand(@"
                                SELECT
                                    (SELECT COUNT(*) FROM copa_partidos WHERE ronda_id = @R AND copa_id = @C) as actual,
                                    (SELECT COUNT(*) FROM copa_partidos WHERE ronda_id = @SR AND copa_id = @C) as siguiente",
                                conn, tx))
                            {
                                cmdCount.Parameters.AddWithValue("@R",  rondaId);
                                cmdCount.Parameters.AddWithValue("@SR", siguienteRondaId.Value);
                                cmdCount.Parameters.AddWithValue("@C",  copaId);
                                using var rc = cmdCount.ExecuteReader();
                                if (rc.Read()) {
                                    partidosRondaActual    = Convert.ToInt32(rc.GetInt64(0));
                                    partidosRondaSiguiente = Convert.ToInt32(rc.GetInt64(1));
                                }
                            }

                            int posSiguiente;
                            string campoCheck;

                            if (partidosRondaActual == partidosRondaSiguiente)
                            {
                                // Relación 1:1 (ej: FP2 8 partidos → Octavos 8 partidos)
                                // Ganador de posición N va a posición N como visitante
                                posSiguiente = posicion;
                                campoCheck   = "equipo_visitante_id";
                            }
                            else
                            {
                                // Relación 2:1 (ej: 16→8, Cuartos→Semis, Semis→Final)
                                posSiguiente = posicion / 2;
                                bool esLocal = posicion % 2 == 0;
                                campoCheck   = esLocal ? "equipo_local_id" : "equipo_visitante_id";
                            }

                            using var cmdAdv = new NpgsqlCommand($@"
                                INSERT INTO copa_partidos (ronda_id, copa_id, equipo_local_id, equipo_visitante_id, jugado, posicion_bracket)
                                SELECT @SR, @C, NULL, NULL, false, @PS
                                WHERE NOT EXISTS (
                                    SELECT 1 FROM copa_partidos 
                                    WHERE ronda_id = @SR AND copa_id = @C AND posicion_bracket = @PS
                                );
                                UPDATE copa_partidos
                                SET {campoCheck} = @G
                                WHERE ronda_id = @SR
                                  AND posicion_bracket = @PS
                                  AND copa_id = @C
                                  AND {campoCheck} IS NULL", conn, tx);
                            cmdAdv.Parameters.AddWithValue("@G",  ganadorId);
                            cmdAdv.Parameters.AddWithValue("@SR", siguienteRondaId.Value);
                            cmdAdv.Parameters.AddWithValue("@PS", posSiguiente);
                            cmdAdv.Parameters.AddWithValue("@C",  copaId);
                            cmdAdv.ExecuteNonQuery();
                        }
                    }
                }

                tx.Commit();
                return true;
            }
            catch { tx.Rollback(); return false; }
        }

        public bool ToggleRondaHabilitada(int rondaId, bool habilitada)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                "UPDATE copa_rondas SET habilitada=@H WHERE id=@Id", conn);
            cmd.Parameters.AddWithValue("@H",   habilitada);
            cmd.Parameters.AddWithValue("@Id",  rondaId);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        private void InsertarPartidoCopaConUnEquipo(NpgsqlConnection conn, NpgsqlTransaction tx,
            int rondaId, int copaId, int localId, int pos)
        {
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO copa_partidos (ronda_id, copa_id, equipo_local_id, jugado, posicion_bracket)
                VALUES (@R, @C, @L, false, @P)", conn, tx);
            cmd.Parameters.AddWithValue("@R", rondaId);
            cmd.Parameters.AddWithValue("@C", copaId);
            cmd.Parameters.AddWithValue("@L", localId);
            cmd.Parameters.AddWithValue("@P", pos);
            cmd.ExecuteNonQuery();
        }

        private void InsertarPartidoCopa(NpgsqlConnection conn, NpgsqlTransaction tx,
            int rondaId, int copaId, int localId, int visitanteId, int pos)
        {
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO copa_partidos (ronda_id, copa_id, equipo_local_id, equipo_visitante_id, jugado, posicion_bracket)
                VALUES (@R, @C, @L, @V, false, @P)", conn, tx);
            cmd.Parameters.AddWithValue("@R", rondaId);
            cmd.Parameters.AddWithValue("@C", copaId);
            cmd.Parameters.AddWithValue("@L", localId > 0 ? (object)localId : DBNull.Value);
            cmd.Parameters.AddWithValue("@V", visitanteId > 0 ? (object)visitanteId : DBNull.Value);
            cmd.Parameters.AddWithValue("@P", pos);
            cmd.ExecuteNonQuery();
        }

        private void InsertarPartidoCopaVacio(NpgsqlConnection conn, NpgsqlTransaction tx,
            int rondaId, int copaId, int pos)
        {
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO copa_partidos (ronda_id, copa_id, jugado, posicion_bracket)
                VALUES (@R, @C, false, @P)", conn, tx);
            cmd.Parameters.AddWithValue("@R", rondaId);
            cmd.Parameters.AddWithValue("@C", copaId);
            cmd.Parameters.AddWithValue("@P", pos);
            cmd.ExecuteNonQuery();
        }

        private static Temporada MapTemporada(IDataReader r) => new()
        {
            Id = r.GetInt32(0), Numero = r.GetInt32(1), Nombre = r.GetString(2),
            FechaInicio = r.IsDBNull(3) ? null : r.GetDateTime(3),
            FechaFin    = r.IsDBNull(4) ? null : r.GetDateTime(4),
            Activa = r.GetBoolean(5), Finalizada = r.GetBoolean(6),
            CantDescensos       = r.FieldCount > 7  && !r.IsDBNull(7)  ? r.GetInt32(7)  : 2,
            CantAscensos        = r.FieldCount > 8  && !r.IsDBNull(8)  ? r.GetInt32(8)  : 2,
            TienePromocion      = r.FieldCount > 9  && !r.IsDBNull(9)  && r.GetBoolean(9),
            PosPromocionPrimera = r.FieldCount > 10 && !r.IsDBNull(10) ? r.GetInt32(10) : null,
            PosPromocionB       = r.FieldCount > 11 && !r.IsDBNull(11) ? r.GetInt32(11) : null,
        };

        private static Copa MapCopa(IDataReader r) => new()
        {
            Id = r.GetInt32(0), TemporadaId = r.IsDBNull(1) ? null : r.GetInt32(1),
            Tipo = r.GetString(2), Nombre = r.GetString(3), Finalizada = r.GetBoolean(4)
        };

        public void CargarPartidoEspecial(int localId, int visitanteId, int golesLocal, int golesVisitante, string torneo, string temporadaNombre)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO enfrentamientos_historicos
                    (equipo_local_id, equipo_visitante_id, goles_local, goles_visitante, torneo, temporada_nombre, division_id)
                VALUES (@L, @V, @GL, @GV, @T, @TN, 0)", conn);
            cmd.Parameters.AddWithValue("@L",  localId);
            cmd.Parameters.AddWithValue("@V",  visitanteId);
            cmd.Parameters.AddWithValue("@GL", golesLocal);
            cmd.Parameters.AddWithValue("@GV", golesVisitante);
            cmd.Parameters.AddWithValue("@T",  torneo);
            cmd.Parameters.AddWithValue("@TN", temporadaNombre);
            conn.Open();
            cmd.ExecuteNonQuery();
        }

        // ── PALMARÉS ───────────────────────────────────

        public void BorrarTitulo(string tipoTitulo, int? temporadaId)
        {
            if (!temporadaId.HasValue) return;
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                "DELETE FROM palmares WHERE tipo_titulo = @T AND temporada_id = @TId", conn);
            cmd.Parameters.AddWithValue("@T",   tipoTitulo);
            cmd.Parameters.AddWithValue("@TId", temporadaId.Value);
            conn.Open();
            cmd.ExecuteNonQuery();
        }

        public void AgregarTitulo(int equipoId, string tipoTitulo, string nombreTitulo, int? temporadaId, string temporadaNombre)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(@"
                INSERT INTO palmares (equipo_id, nombre_equipo, tipo_titulo, nombre_titulo, temporada_id, temporada_nombre)
                VALUES (@E, (SELECT nombre FROM equipos WHERE id = @E), @T, @N, @TId, @TNom)", conn);
            cmd.Parameters.AddWithValue("@E",    equipoId);
            cmd.Parameters.AddWithValue("@T",    tipoTitulo);
            cmd.Parameters.AddWithValue("@N",    nombreTitulo);
            cmd.Parameters.AddWithValue("@TId",  (object?)temporadaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TNom", temporadaNombre);
            conn.Open();
            cmd.ExecuteNonQuery();
        }

        // ── TROFEOS (VIDRIERA) ──────────────────────────
        public List<RankingAllTimeEntry> GetRankingAllTime()
        {
            // Verificar si existe la tabla de histórico antes de usarla
            bool tablaHistoricoExiste = false;
            using (var connChk = GetConnection())
            {
                connChk.Open();
                using var chk = new NpgsqlCommand(
                    "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='enfrentamientos_historicos')", connChk);
                tablaHistoricoExiste = (bool)(chk.ExecuteScalar() ?? false);
            }

            // Sistema de puntos:
            // Victoria 5-0/5-1/5-2/5-3 = 3pts ganador, 0pts perdedor
            // Victoria 5-4             = 2pts ganador, 1pt  perdedor
            // Fuentes: tabla 'partidos' (div=1, excluyendo histórico y copas)
            //          tabla 'enfrentamientos_historicos' (torneo = 'Liga', division_id=1) si existe
            var sql = @"
                WITH todos_los_partidos AS (
                    -- Partidos del sistema (Primera División, excluyendo fecha histórica)
                    SELECT
                        p.equipolocalid    AS local_id,
                        p.equipovisitanteid AS visit_id,
                        p.goleslocal       AS gl,
                        p.golesvisitante   AS gv
                    FROM partidos p
                    WHERE p.divisionid = 1
                      AND p.jugado = true
                      AND p.fechaid NOT IN (SELECT id FROM fechas WHERE nombre = 'Histórico Pre-App')

                    @UNION_HISTORICO@
                ),
                puntos_por_partido AS (
                    SELECT
                        local_id AS equipo_id,
                        gl AS goles_favor,
                        gv AS goles_contra,
                        CASE
                            WHEN gl > gv AND (gl - gv) >= 2 THEN 3  -- 5-0/5-1/5-2/5-3
                            WHEN gl > gv AND (gl - gv) = 1  THEN 2  -- 5-4
                            WHEN gl < gv AND (gv - gl) = 1  THEN 1  -- perdió 4-5
                            ELSE 0
                        END AS puntos
                    FROM todos_los_partidos

                    UNION ALL

                    SELECT
                        visit_id AS equipo_id,
                        gv AS goles_favor,
                        gl AS goles_contra,
                        CASE
                            WHEN gv > gl AND (gv - gl) >= 2 THEN 3
                            WHEN gv > gl AND (gv - gl) = 1  THEN 2
                            WHEN gv < gl AND (gl - gv) = 1  THEN 1
                            ELSE 0
                        END AS puntos
                    FROM todos_los_partidos
                )
                SELECT
                    e.id,
                    e.nombre,
                    COALESCE(e.pais_code, '') AS pais_code,
                    COUNT(*)                                              AS partidos_jugados,
                    COUNT(*) FILTER (WHERE pp.goles_favor > pp.goles_contra) AS victorias,
                    COUNT(*) FILTER (WHERE pp.goles_favor < pp.goles_contra) AS derrotas,
                    COALESCE(SUM(pp.goles_favor),  0)                    AS goles_a_favor,
                    COALESCE(SUM(pp.goles_contra), 0)                    AS goles_en_contra,
                    COALESCE(SUM(pp.puntos), 0)                          AS puntos_total
                FROM equipos e
                JOIN puntos_por_partido pp ON pp.equipo_id = e.id
                GROUP BY e.id, e.nombre, e.pais_code
                HAVING COUNT(*) > 0
                ORDER BY puntos_total DESC, victorias DESC, goles_a_favor DESC";

            var lista = new List<RankingAllTimeEntry>();
            var unionHistorico = tablaHistoricoExiste ? @"
                    UNION ALL
                    SELECT eh.equipo_local_id, eh.equipo_visitante_id, eh.goles_local, eh.goles_visitante
                    FROM enfrentamientos_historicos eh
                    WHERE eh.torneo = 'Liga' AND eh.division_id = 1" : "";

            sql = sql.Replace("@UNION_HISTORICO@", unionHistorico);

            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(sql, conn);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var nombre   = r.GetString(1);
                var paisCode = r.GetString(2);
                lista.Add(new RankingAllTimeEntry
                {
                    EquipoId        = r.GetInt32(0),
                    NombreEquipo    = nombre,
                    FlagCode        = !string.IsNullOrEmpty(paisCode) ? paisCode : BanderaMap.GetCode(nombre),
                    PartidosJugados = Convert.ToInt32(r.GetInt64(3)),
                    Victorias       = Convert.ToInt32(r.GetInt64(4)),
                    Derrotas        = Convert.ToInt32(r.GetInt64(5)),
                    GolesAFavor     = Convert.ToInt32(r.GetInt64(6)),
                    GolesEnContra   = Convert.ToInt32(r.GetInt64(7)),
                    PuntosTotal     = Convert.ToInt32(r.GetInt64(8))
                });
            }
            return lista;
        }


        // ── CONFIGURACIÓN GLOBAL ────────────────────────
        public string GetConfig(string clave, string defaultVal = "false")
        {
            try {
                using var conn = GetConnection();
                using var cmd  = new NpgsqlCommand("SELECT valor FROM configuracion_global WHERE clave = @C", conn);
                cmd.Parameters.AddWithValue("@C", clave);
                conn.Open();
                return cmd.ExecuteScalar()?.ToString() ?? defaultVal;
            } catch { return defaultVal; }
        }

        public void SetConfig(string clave, string valor)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(@"
                INSERT INTO configuracion_global (clave, valor, updated_at)
                VALUES (@C, @V, NOW())
                ON CONFLICT (clave) DO UPDATE SET valor = @V, updated_at = NOW()", conn);
            cmd.Parameters.AddWithValue("@C", clave);
            cmd.Parameters.AddWithValue("@V", valor);
            conn.Open();
            cmd.ExecuteNonQuery();
        }

        public bool PrimeraCActiva() => GetConfig("primera_c_activa") == "true";
        public bool PrimeraCIdaVuelta() => GetConfig("primera_c_ida_vuelta") == "true";

        // ── HISTORIAL LEGACY ─────────────────────────────
        public List<LegacyTemporada> GetLegacyTemporadas()
        {
            const string sql = @"
                SELECT
                    eh.equipo_local_id, eh.equipo_visitante_id,
                    eh.goles_local, eh.goles_visitante,
                    eh.temporada_nombre,
                    el.nombre as nombre_local, COALESCE(el.pais_code,'') as flag_local,
                    ev.nombre as nombre_visit, COALESCE(ev.pais_code,'') as flag_visit
                FROM enfrentamientos_historicos eh
                JOIN equipos el ON eh.equipo_local_id = el.id
                JOIN equipos ev ON eh.equipo_visitante_id = ev.id
                WHERE eh.torneo = 'Liga' AND eh.division_id = 1
                  AND eh.temporada_nombre LIKE 'Temporada %'
                  AND CAST(REGEXP_REPLACE(eh.temporada_nombre, '[^0-9]', '', 'g') AS INT) <= 16
                ORDER BY eh.temporada_nombre, eh.id";

            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(sql, conn);
            conn.Open();
            using var r = cmd.ExecuteReader();

            var dict = new Dictionary<string, List<(int lId, int vId, string lN, string lF, string vN, string vF, int gl, int gv)>>();
            while (r.Read())
            {
                var tempNombre = r.GetString(4);
                if (!dict.ContainsKey(tempNombre)) dict[tempNombre] = new();
                dict[tempNombre].Add((
                    r.GetInt32(0), r.GetInt32(1),
                    r.GetString(5), r.GetString(6),
                    r.GetString(7), r.GetString(8),
                    r.GetInt32(2), r.GetInt32(3)
                ));
            }
            r.Close();

            var resultado = new List<LegacyTemporada>();
            foreach (var (tempNombre, partidos) in dict.OrderBy(x => x.Key))
            {
                var numMatch = System.Text.RegularExpressions.Regex.Match(tempNombre, @"\d+");
                int numero = numMatch.Success ? int.Parse(numMatch.Value) : 0;

                var stats = new Dictionary<int, (string nombre, string flag, int pj, int v, int d, int gf, int gc, int pts)>();

                void AddStats(int id, string nombre, string flag, int gf, int gc)
                {
                    if (!stats.ContainsKey(id)) stats[id] = (nombre, flag, 0,0,0,0,0,0);
                    var s = stats[id];
                    stats[id] = (s.nombre, s.flag, s.pj+1, s.v, s.d, s.gf+gf, s.gc+gc, s.pts);
                }

                var fechas = new Dictionary<int, List<LegacyPartido>>();
                int fechaNum = 1;
                foreach (var p in partidos)
                {
                    AddStats(p.lId, p.lN, p.lF, p.gl, p.gv);
                    AddStats(p.vId, p.vN, p.vF, p.gv, p.gl);

                    int diff = Math.Abs(p.gl - p.gv);
                    var sl = stats[p.lId]; var sv = stats[p.vId];
                    if (p.gl > p.gv)
                    {
                        stats[p.lId] = (sl.nombre, sl.flag, sl.pj, sl.v+1, sl.d, sl.gf, sl.gc, sl.pts + (diff >= 2 ? 3 : 2));
                        stats[p.vId] = (sv.nombre, sv.flag, sv.pj, sv.v, sv.d+1, sv.gf, sv.gc, sv.pts + (diff == 1 ? 1 : 0));
                    }
                    else
                    {
                        stats[p.vId] = (sv.nombre, sv.flag, sv.pj, sv.v+1, sv.d, sv.gf, sv.gc, sv.pts + (diff >= 2 ? 3 : 2));
                        stats[p.lId] = (sl.nombre, sl.flag, sl.pj, sl.v, sl.d+1, sl.gf, sl.gc, sl.pts + (diff == 1 ? 1 : 0));
                    }

                    if (!fechas.ContainsKey(fechaNum)) fechas[fechaNum] = new();
                    fechas[fechaNum].Add(new LegacyPartido
                    {
                        EquipoLocalId = p.lId, NombreLocal = p.lN, FlagLocal = p.lF,
                        EquipoVisitanteId = p.vId, NombreVisitante = p.vN, FlagVisitante = p.vF,
                        GolesLocal = p.gl, GolesVisitante = p.gv
                    });
                    if (fechas[fechaNum].Count >= 5) fechaNum++;
                }

                var tabla = stats.Values
                    .Select(s => new LegacyPosicion
                    {
                        EquipoId = stats.First(x => x.Value.nombre == s.nombre).Key,
                        NombreEquipo = s.nombre,
                        FlagCode = !string.IsNullOrEmpty(s.flag) ? s.flag : BanderaMap.GetCode(s.nombre),
                        PJ = s.pj, V = s.v, D = s.d, GF = s.gf, GC = s.gc, Pts = s.pts
                    })
                    .OrderByDescending(x => x.Pts).ThenByDescending(x => x.V)
                    .ThenByDescending(x => x.GF - x.GC).ToList();

                resultado.Add(new LegacyTemporada
                {
                    Numero = numero,
                    Tabla = tabla,
                    TotalPartidos = partidos.Count,
                    Fechas = fechas.OrderBy(f => f.Key).Select(f => new LegacyFecha
                    {
                        Numero = f.Key,
                        Partidos = f.Value
                    }).ToList()
                });
            }
            return resultado.OrderBy(t => t.Numero).ToList();
        }

        public List<RankingFifaEntry> GetRankingFifa(int ultimasTemporadas = 5)
        {
            // Obtener IDs de las últimas N temporadas finalizadas
            const string sqlTemps = @"
                SELECT id FROM temporadas 
                WHERE finalizada = true 
                ORDER BY numero DESC 
                LIMIT @N";

            const string sqlPuntos = @"
                WITH puntos_base AS (
                    -- Puntos por posición en tabla final (solo Primera División)
                    SELECT
                        tr.equipo_id,
                        tr.temporada_id,
                        CASE
                            WHEN tr.division_id = 1 AND tr.posicion = 1 THEN 30
                            WHEN tr.division_id = 1 AND tr.posicion = 2 THEN 10
                            WHEN tr.division_id = 1 AND tr.posicion = 3 THEN 5
                            ELSE 0
                        END as pts_posicion
                    FROM temporada_resultados tr
                    WHERE tr.temporada_id = ANY(@Temps)
                      AND tr.division_id = 1

                    UNION ALL

                    -- Puntos por títulos en palmares
                    SELECT
                        p.equipo_id,
                        p.temporada_id,
                        CASE p.tipo_titulo
                            WHEN 'campeon_copa'      THEN 25
                            WHEN 'campeon_supercopa' THEN 20
                            WHEN 'campeon_primera_b' THEN 5
                            ELSE 0
                        END as pts_posicion
                    FROM palmares p
                    WHERE p.temporada_id = ANY(@Temps)
                      AND p.equipo_id IS NOT NULL
                      AND p.tipo_titulo IN ('campeon_copa','campeon_supercopa')
                )
                SELECT
                    e.id,
                    e.nombre,
                    COALESCE(e.pais_code,'') as pais_code,
                    COALESCE(SUM(pb.pts_posicion), 0) as puntos_total,
                    COUNT(*) FILTER (WHERE pb.pts_posicion = 30) as titulos_liga,
                    COUNT(*) FILTER (WHERE pb.pts_posicion = 25) as titulos_copa,
                    COUNT(*) FILTER (WHERE pb.pts_posicion = 20) as titulos_supercopa
                FROM equipos e
                JOIN puntos_base pb ON pb.equipo_id = e.id
                GROUP BY e.id, e.nombre, e.pais_code
                HAVING SUM(pb.pts_posicion) > 0
                ORDER BY puntos_total DESC, titulos_liga DESC";

            using var conn = GetConnection();
            conn.Open();

            // Get temporada IDs
            var tempIds = new List<int>();
            using (var cmd = new NpgsqlCommand(sqlTemps, conn))
            {
                cmd.Parameters.AddWithValue("@N", ultimasTemporadas);
                using var r = cmd.ExecuteReader();
                while (r.Read()) tempIds.Add(r.GetInt32(0));
            }
            if (!tempIds.Any()) return new();

            var lista = new List<RankingFifaEntry>();
            using (var cmd = new NpgsqlCommand(sqlPuntos, conn))
            {
                cmd.Parameters.AddWithValue("@Temps", tempIds.ToArray());
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var nombre = r.GetString(1);
                    var paisCode = r.GetString(2);
                    lista.Add(new RankingFifaEntry
                    {
                        EquipoId              = r.GetInt32(0),
                        NombreEquipo          = nombre,
                        FlagCode              = !string.IsNullOrEmpty(paisCode) ? paisCode : BanderaMap.GetCode(nombre),
                        PuntosTotal           = Convert.ToInt32(r.GetInt64(3)),
                        TitulosLiga           = Convert.ToInt32(r.GetInt64(4)),
                        TitulosCopa           = Convert.ToInt32(r.GetInt64(5)),
                        TitulosSupercopa      = Convert.ToInt32(r.GetInt64(6)),
                        TemporadasConsideradas = tempIds.Count
                    });
                }
            }
            return lista;
        }

        public List<HistoricoPartido> GetPartidosHistorico(string temporadaNombre, int divisionId)
        {
            const string sql = @"
                SELECT eh.goles_local, eh.goles_visitante, eh.torneo,
                       el.nombre, COALESCE(el.pais_code,''), el.id,
                       ev.nombre, COALESCE(ev.pais_code,''), ev.id,
                       COALESCE(eh.fecha_numero, 0)
                FROM enfrentamientos_historicos eh
                JOIN equipos el ON eh.equipo_local_id = el.id
                JOIN equipos ev ON eh.equipo_visitante_id = ev.id
                WHERE eh.temporada_nombre = @T AND eh.division_id = @D
                ORDER BY COALESCE(eh.fecha_numero, 0), eh.id";
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@T", temporadaNombre);
            cmd.Parameters.AddWithValue("@D", divisionId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            var lista = new List<HistoricoPartido>();
            while (r.Read())
                lista.Add(new HistoricoPartido
                {
                    GolesLocal        = r.GetInt32(0),
                    GolesVisitante    = r.GetInt32(1),
                    Torneo            = r.GetString(2),
                    NombreLocal       = r.GetString(3),
                    FlagLocal         = r.GetString(4),
                    EquipoLocalId     = r.GetInt32(5),
                    NombreVisitante   = r.GetString(6),
                    FlagVisitante     = r.GetString(7),
                    EquipoVisitanteId = r.GetInt32(8),
                    FechaNumero       = r.GetInt32(9)
                });
            return lista;
        }

        public List<Trofeo> GetTrofeos()
        {
            var lista = new List<Trofeo>();
            using var conn = GetConnection();
            conn.Open();

            using (var cmd = new NpgsqlCommand(
                "SELECT id, nombre, imagen_url, tipo_titulo, orden FROM trofeos ORDER BY orden", conn))
            {
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    lista.Add(new Trofeo
                    {
                        Id         = r.GetInt32(0),
                        Nombre     = r.GetString(1),
                        ImagenUrl  = r.IsDBNull(2) ? null : r.GetString(2),
                        TipoTitulo = r.GetString(3),
                        Orden      = r.GetInt32(4)
                    });
                }
            }

            // Cargar campeones por tipo
            foreach (var trofeo in lista)
            {
                using var cmd = new NpgsqlCommand(@"
                    SELECT p.id, p.nombre_equipo, COALESCE(p.equipo_id,0), p.tipo_titulo, p.nombre_titulo, p.temporada_id, p.temporada_nombre
                    FROM palmares p
                    LEFT JOIN temporadas t ON p.temporada_id = t.id
                    WHERE p.tipo_titulo = @T
                    ORDER BY
                        CASE WHEN REGEXP_REPLACE(p.temporada_nombre, '[^0-9]', '', 'g') = '' THEN NULL
                             ELSE CAST(REGEXP_REPLACE(p.temporada_nombre, '[^0-9]', '', 'g') AS INTEGER)
                        END DESC NULLS LAST,
                        p.created_at DESC", conn);
                cmd.Parameters.AddWithValue("@T", trofeo.TipoTitulo);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var nombre = r.GetString(1);
                    trofeo.Campeones.Add(new TorneoAmigos.Models.Titulo
                    {
                        Id              = r.GetInt32(0),
                        NombreEquipo    = nombre,
                        FlagCode        = BanderaMap.GetCode(nombre),
                        EquipoId        = r.GetInt32(2),
                        TipoTitulo      = r.GetString(3),
                        NombreTitulo    = r.GetString(4),
                        TemporadaId     = r.IsDBNull(5) ? null : r.GetInt32(5),
                        TemporadaNombre = r.GetString(6)
                    });
                }
            }

            return lista;
        }

        public bool ActualizarImagenTrofeo(int trofeoId, string imagenUrl)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand("UPDATE trofeos SET imagen_url = @U WHERE id = @Id", conn);
            cmd.Parameters.AddWithValue("@U",  imagenUrl);
            cmd.Parameters.AddWithValue("@Id", trofeoId);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        public List<TorneoAmigos.Models.Titulo> GetTitulosPorEquipo(string nombreEquipo)
        {
            var titulos = new List<TorneoAmigos.Models.Titulo>();
            // Buscar coincidencia exacta O variantes históricas (ej: "Pato" también matchea "Pato L")
            var sql = @"
                SELECT id, tipo_titulo, nombre_titulo, temporada_id, temporada_nombre, nombre_equipo
                FROM palmares
                WHERE LOWER(TRIM(nombre_equipo)) = LOWER(TRIM(@N))
                   OR LOWER(TRIM(nombre_equipo)) LIKE LOWER(TRIM(@N)) || ' %'
                ORDER BY created_at DESC";

            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@N", nombreEquipo);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                titulos.Add(new TorneoAmigos.Models.Titulo
                {
                    Id              = r.GetInt32(0),
                    TipoTitulo      = r.GetString(1),
                    NombreTitulo    = r.GetString(2),
                    TemporadaId     = r.IsDBNull(3) ? null : r.GetInt32(3),
                    TemporadaNombre = r.GetString(4)
                });
            }
            return titulos;
        }

        public PalmaresViewModel GetPalmares()
        {
            var titulos = new List<TorneoAmigos.Models.Titulo>();
            // nombre_equipo puede venir directo de la tabla (histórico) o de JOIN con equipos
            var sql = @"
                SELECT p.id,
                       COALESCE(p.equipo_id, 0),
                       COALESCE(p.nombre_equipo, e.nombre, 'Desconocido'),
                       p.tipo_titulo, p.nombre_titulo,
                       p.temporada_id, p.temporada_nombre
                FROM palmares p
                LEFT JOIN equipos e ON p.equipo_id = e.id
                ORDER BY p.created_at DESC";

            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(sql, conn);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var nombre = r.GetString(2);
                titulos.Add(new TorneoAmigos.Models.Titulo
                {
                    Id              = r.GetInt32(0),
                    EquipoId        = r.GetInt32(1),
                    NombreEquipo    = nombre,
                    FlagCode        = BanderaMap.GetCode(nombre),
                    TipoTitulo      = r.GetString(3),
                    NombreTitulo    = r.GetString(4),
                    TemporadaId     = r.IsDBNull(5) ? null : r.GetInt32(5),
                    TemporadaNombre = r.GetString(6)
                });
            }

            // Agrupar por nombre (los históricos tienen equipo_id = 0)
            var equipos = titulos
                .GroupBy(t => t.NombreEquipo.Trim().ToLower())
                .Select(g => new TorneoAmigos.Models.PalmaresEquipo
                {
                    EquipoId        = g.First().EquipoId,
                    NombreEquipo    = g.First().NombreEquipo,
                    FlagCode        = g.First().FlagCode,
                    TotalTitulos    = g.Count(),
                    CampeonatosLiga = g.Count(t => t.TipoTitulo == "campeon_torneo"),
                    CopaArgentina   = g.Count(t => t.TipoTitulo == "campeon_copa"),
                    Supercopa       = g.Count(t => t.TipoTitulo == "campeon_supercopa"),
                    Titulos         = g.ToList()
                })
                .OrderByDescending(e => e.TotalTitulos)
                .ThenByDescending(e => e.CampeonatosLiga)
                .ToList();

            return new TorneoAmigos.Models.PalmaresViewModel
            {
                Equipos        = equipos,
                UltimosTitulos = titulos.Take(10).ToList()
            };
        }
    }
}
