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
                       tc.campeon_b_id,          eb.nombre
                FROM temporada_cierre tc
                LEFT JOIN equipos ec  ON tc.campeon_copa_id      = ec.id
                LEFT JOIN equipos es  ON tc.campeon_supercopa_id = es.id
                LEFT JOIN equipos ea1 ON tc.ascenso_1_id         = ea1.id
                LEFT JOIN equipos ea2 ON tc.ascenso_2_id         = ea2.id
                LEFT JOIN equipos ed1 ON tc.descenso_1_id        = ed1.id
                LEFT JOIN equipos ed2 ON tc.descenso_2_id        = ed2.id
                LEFT JOIN equipos ep  ON tc.campeon_primera_id   = ep.id
                LEFT JOIN equipos eb  ON tc.campeon_b_id         = eb.id
                WHERE tc.temporada_id = @T";
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@T", temporadaId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            // índices: 0=id, 1=temporada_id,
            //          2=copa_id, 3=copa_nombre,
            //          4=supercopa_id, 5=supercopa_nombre,
            //          6=asc1_id, 7=asc1_nombre,
            //          8=asc2_id, 9=asc2_nombre,
            //          10=desc1_id, 11=desc1_nombre,
            //          12=desc2_id, 13=desc2_nombre,
            //          14=sin_descensos,
            //          15=primera_id, 16=primera_nombre,
            //          17=b_id, 18=b_nombre
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
                CampeonBId             = r.IsDBNull(17) ? null : r.GetInt32(17)
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
                     ascenso_1_id, ascenso_2_id, descenso_1_id, descenso_2_id,
                     sin_descensos, updated_at)
                VALUES (@T, @CC, @CS, @CP, @CB, @A1, @A2, @D1, @D2, @SD, NOW())
                ON CONFLICT (temporada_id) DO UPDATE SET
                    campeon_copa_id      = EXCLUDED.campeon_copa_id,
                    campeon_supercopa_id = EXCLUDED.campeon_supercopa_id,
                    campeon_primera_id   = EXCLUDED.campeon_primera_id,
                    campeon_b_id         = EXCLUDED.campeon_b_id,
                    ascenso_1_id         = EXCLUDED.ascenso_1_id,
                    ascenso_2_id         = EXCLUDED.ascenso_2_id,
                    descenso_1_id        = EXCLUDED.descenso_1_id,
                    descenso_2_id        = EXCLUDED.descenso_2_id,
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
            cmd.Parameters.AddWithValue("@D1", (object?)cierre.Descenso1Id        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@D2", (object?)cierre.Descenso2Id        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SD", cierre.SinDescensos);
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
                        (equipo_local_id, equipo_visitante_id, goles_local, goles_visitante, torneo, temporada_nombre, division_id)
                    SELECT p.equipolocalid, p.equipovisitanteid, p.goleslocal, p.golesvisitante,
                           'Liga', @TNom, p.divisionid
                    FROM partidos p
                    WHERE p.jugado = true
                      AND COALESCE(p.tipo_partido, 'regular') = 'regular'
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
                if (ascenso1 > 0) EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 1 WHERE id = @Id", ascenso1);
                if (ascenso2 > 0) EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 1 WHERE id = @Id", ascenso2);

                // Descensos: usar borrador si existe, si no usar los 2 últimos de tabla Primera
                if (!sinDescensos)
                {
                    var descenso1 = cierre?.Descenso1Id ?? (tablaPrimera.Count >= 2 ? tablaPrimera[tablaPrimera.Count - 2].EquipoId : 0);
                    var descenso2 = cierre?.Descenso2Id ?? (tablaPrimera.Count >= 1 ? tablaPrimera[tablaPrimera.Count - 1].EquipoId : 0);
                    if (descenso1 > 0) EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 2 WHERE id = @Id", descenso1);
                    if (descenso2 > 0) EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 2 WHERE id = @Id", descenso2);
                }

                // Registrar títulos en el palmarés (temporadaNombre ya fue obtenido arriba)

                // Campeón Primera División: usar borrador si existe, si no el 1° de tabla
                var campeonPrimId = cierre?.CampeonPrimeraId ?? (tablaPrimera.Any() ? tablaPrimera.First().EquipoId : 0);
                if (campeonPrimId > 0)
                    AgregarTitulo(campeonPrimId, "campeon_torneo", "Campeón Primera División", temporadaId, temporadaNombre);

                // Campeón de la B: usar borrador si existe, si no el 1° de tabla B
                var campeonBId = cierre?.CampeonBId ?? (tablaB.Any() ? tablaB.First().EquipoId : 0);
                if (campeonBId > 0)
                    AgregarTitulo(campeonBId, "campeon_primera_b", "Campeón Primera Nacional", temporadaId, temporadaNombre);

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
                bool descendio = !sinDescensos && divisionId == 1 && i >= totalEquipos - 2;
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
            int? posPromocionPrimera = null, int? posPromocionB = null)
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

            using var tx = conn.BeginTransaction();
            BorrarFixture(conn, tx);
            if (equiposPrimera.Any()) GenerarFixture(conn, tx, 1, equiposPrimera);
            if (equiposB.Any())       GenerarFixture(conn, tx, 2, equiposB);
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

        private void GenerarFixture(NpgsqlConnection conn, NpgsqlTransaction tx, int divisionId, List<int> equipos)
        {
            if (equipos.Count < 2) return;

            // Algoritmo round-robin (todos contra todos, ida)
            var lista = new List<int>(equipos);
            if (lista.Count % 2 != 0) lista.Add(-1); // bye si es impar
            int n = lista.Count;
            int numFechas = n - 1;

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

            // Habilitar fecha 1 automáticamente
            using (var cmd = new NpgsqlCommand("UPDATE fechas SET habilitada = true WHERE id = @Id", conn, tx))
            {
                cmd.Parameters.AddWithValue("@Id", fechaIds[0]);
                cmd.ExecuteNonQuery();
            }

            // Round robin
            for (int fecha = 0; fecha < numFechas; fecha++)
            {
                int fechaId = fechaIds[fecha];
                for (int i = 0; i < n / 2; i++)
                {
                    int local    = lista[i];
                    int visitante = lista[n - 1 - i];
                    if (local == -1 || visitante == -1) continue; // bye

                    using var cmd = new NpgsqlCommand(@"
                        INSERT INTO partidos (fechaid, divisionid, equipolocalid, equipovisitanteid, jugado)
                        VALUES (@F, @D, @L, @V, false)", conn, tx);
                    cmd.Parameters.AddWithValue("@F", fechaId);
                    cmd.Parameters.AddWithValue("@D", divisionId);
                    cmd.Parameters.AddWithValue("@L", local);
                    cmd.Parameters.AddWithValue("@V", visitante);
                    cmd.ExecuteNonQuery();
                }
                // Rotar lista (mantener el primero fijo)
                var ultimo = lista[lista.Count - 1];
                lista.RemoveAt(lista.Count - 1);
                lista.Insert(1, ultimo);
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
            List<int>? equiposNuevosB = null)
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

                // ── LÓGICA DEL SORTEO ──────────────────────────────────────────
                // Estructura fija:
                // FP1: S10-S15 entre sí (3 partidos, 3 ganadores)
                // FP2: P9-P12 vs S5-S8, S1 vs S9, S2-S4 vs ganadores FP1 (8 partidos)
                // Octavos: P1-P8 vs ganadores FP2 (8 partidos)

                var rng = new Random();
                int nP = equiposPrimera.Count; // ordenados mejor→peor: P1, P2, ...
                int nS = equiposB.Count;        // ordenados mejor→peor: S1, S2, ...

                // Separar equipos por posición
                var P_octavos = equiposPrimera.Take(8).ToList();           // P1-P8 → directo a Octavos
                var P_previa  = equiposPrimera.Skip(8).ToList();           // P9-P12 → FP2

                // S se dividen en 3 grupos:
                // S1-S4 → FP2 (vs ganadores FP1)
                // S5-S9 → FP2 (vs P_previa y S1)
                // S10+ → FP1
                int cantFP1 = Math.Max(0, nS - 9);  // los que van a FP1
                if (cantFP1 % 2 != 0) cantFP1--;    // asegurar par

                var S_fp1   = equiposB.Skip(nS - cantFP1).ToList(); // los peores van a FP1
                var S_resto = equiposB.Take(nS - cantFP1).ToList(); // los demás van a FP2

                // De S_resto: los primeros 4 (S1-S4) esperan ganadores FP1
                // El resto (S5-S9 + lo que sobre) van contra P_previa y entre sí
                int ganFP1   = cantFP1 / 2;
                var S_vs_FP1 = S_resto.Take(ganFP1).ToList();       // S1-S(ganFP1) → vs ganadores FP1
                var S_vs_P   = S_resto.Skip(ganFP1).ToList();        // S(ganFP1+1)+ → vs P_previa o entre sí

                // Mezclar aleatoriamente dentro de cada grupo
                S_fp1   = S_fp1.OrderBy(_   => rng.Next()).ToList();
                S_vs_FP1 = S_vs_FP1.OrderBy(_ => rng.Next()).ToList();
                S_vs_P   = S_vs_P.OrderBy(_   => rng.Next()).ToList();
                var P_prev_rand = P_previa.OrderBy(_ => rng.Next()).ToList();

                // ── CREAR RONDAS ─────────────────────────────────────────────
                var rondas = new List<(string nombre, int orden, bool habilitada)>();
                if (S_fp1.Any())  rondas.Add(("Fase Previa 1", 1, true));
                rondas.Add(("Fase Previa 2", 2, !S_fp1.Any()));
                rondas.Add(("Octavos de Final", 3, false));
                rondas.Add(("Cuartos de Final", 4, false));
                rondas.Add(("Semifinales",      5, false));
                rondas.Add(("Final",            6, false));

                var rondaIds = new Dictionary<string, int>();
                foreach (var (nombre, orden, habilitada) in rondas)
                {
                    using var cmd = new NpgsqlCommand(
                        "INSERT INTO copa_rondas (copa_id, nombre, orden, habilitada) VALUES (@C, @N, @O, @H) RETURNING id", conn, tx);
                    cmd.Parameters.AddWithValue("@C", copaId);
                    cmd.Parameters.AddWithValue("@N", nombre);
                    cmd.Parameters.AddWithValue("@O", orden);
                    cmd.Parameters.AddWithValue("@H", habilitada);
                    rondaIds[nombre] = Convert.ToInt32(cmd.ExecuteScalar());
                }

                int pos = 0;

                // ── FASE PREVIA 1: los peores de S entre sí ─────────────────
                if (S_fp1.Any() && rondaIds.ContainsKey("Fase Previa 1"))
                {
                    pos = 0;
                    for (int i = 0; i + 1 < S_fp1.Count; i += 2)
                        InsertarPartidoCopa(conn, tx, rondaIds["Fase Previa 1"], copaId, S_fp1[i], S_fp1[i + 1], i / 2);
                }

                // ── FASE PREVIA 2: 8 partidos ───────────────────────────────
                if (rondaIds.ContainsKey("Fase Previa 2"))
                {
                    pos = 0;

                    // P_previa vs S_vs_P (P9 vs S5, P10 vs S6, etc.)
                    for (int i = 0; i < P_prev_rand.Count && i < S_vs_P.Count; i++)
                        InsertarPartidoCopa(conn, tx, rondaIds["Fase Previa 2"], copaId, P_prev_rand[i], S_vs_P[i], pos++);

                    // S vs S restantes (ej: S1 vs S9 si sobra alguien de S_vs_P)
                    var sVsPRestantes = S_vs_P.Skip(P_prev_rand.Count).ToList();
                    for (int i = 0; i + 1 < sVsPRestantes.Count; i += 2)
                        InsertarPartidoCopa(conn, tx, rondaIds["Fase Previa 2"], copaId, sVsPRestantes[i], sVsPRestantes[i + 1], pos++);

                    // S_vs_FP1 vs slots vacíos (ganadores de FP1)
                    for (int i = 0; i < S_vs_FP1.Count && pos < 8; i++)
                        InsertarPartidoCopaConUnEquipo(conn, tx, rondaIds["Fase Previa 2"], copaId, S_vs_FP1[i], pos++);

                    // Slots completamente vacíos si aún faltan partidos para llegar a 8
                    while (pos < 8)
                        InsertarPartidoCopaVacio(conn, tx, rondaIds["Fase Previa 2"], copaId, pos++);
                }

                // ── OCTAVOS: P1-P8 vs slots vacíos (ganadores FP2) ──────────
                if (rondaIds.ContainsKey("Octavos de Final"))
                {
                    var octP = P_octavos.OrderBy(_ => rng.Next()).ToList();
                    for (int i = 0; i < octP.Count; i++)
                        InsertarPartidoCopaConUnEquipo(conn, tx, rondaIds["Octavos de Final"], copaId, octP[i], i);
                }


