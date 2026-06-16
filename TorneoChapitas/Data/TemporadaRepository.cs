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
                "SELECT id, numero, nombre, fecha_inicio, fecha_fin, activa, finalizada FROM temporadas WHERE activa = true LIMIT 1", conn);
            conn.Open();
            using var r = cmd.ExecuteReader();
            return r.Read() ? MapTemporada(r) : null;
        }

        public List<Temporada> GetTodasLasTemporadas()
        {
            var lista = new List<Temporada>();
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                "SELECT id, numero, nombre, fecha_inicio, fecha_fin, activa, finalizada FROM temporadas ORDER BY numero DESC", conn);
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
                       tc.campeon_primera_id, ep.nombre,
                       tc.campeon_b_id,       eb.nombre,
                       tc.sin_descensos
                FROM temporada_cierre tc
                LEFT JOIN equipos ep  ON tc.campeon_primera_id = ep.id
                LEFT JOIN equipos eb  ON tc.campeon_b_id         = eb.id
                LEFT JOIN equipos ec  ON tc.campeon_copa_id      = ec.id
                LEFT JOIN equipos es  ON tc.campeon_supercopa_id = es.id
                LEFT JOIN equipos ea1 ON tc.ascenso_1_id         = ea1.id
                LEFT JOIN equipos ea2 ON tc.ascenso_2_id         = ea2.id
                LEFT JOIN equipos ed1 ON tc.descenso_1_id        = ed1.id
                LEFT JOIN equipos ed2 ON tc.descenso_2_id        = ed2.id
                WHERE tc.temporada_id = @T";
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@T", temporadaId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new TemporadaCierre
            {
                Id                 = r.GetInt32(0),
                TemporadaId        = r.GetInt32(1),
                CampeonCopaId      = r.IsDBNull(2)  ? null : r.GetInt32(2),
                CampeonCopaNombre  = r.IsDBNull(3)  ? null : r.GetString(3),
                CampeonSupercopaId      = r.IsDBNull(4)  ? null : r.GetInt32(4),
                CampeonSupercoPaNombre  = r.IsDBNull(5)  ? null : r.GetString(5),
                Ascenso1Id         = r.IsDBNull(6)  ? null : r.GetInt32(6),
                Ascenso1Nombre     = r.IsDBNull(7)  ? null : r.GetString(7),
                Ascenso2Id         = r.IsDBNull(8)  ? null : r.GetInt32(8),
                Ascenso2Nombre     = r.IsDBNull(9)  ? null : r.GetString(9),
                Descenso1Id        = r.IsDBNull(10) ? null : r.GetInt32(10),
                Descenso1Nombre    = r.IsDBNull(11) ? null : r.GetString(11),
                Descenso2Id        = r.IsDBNull(12) ? null : r.GetInt32(12),
                Descenso2Nombre    = r.IsDBNull(13) ? null : r.GetString(13),
                SinDescensos       = r.GetBoolean(14),
                CampeonPrimeraId   = r.IsDBNull(15) ? null : r.GetInt32(15),
                CampeonBId         = r.IsDBNull(17) ? null : r.GetInt32(17)
            };
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
                // Guardar resultados Primera División
                GuardarResultados(conn, tx, temporadaId, 1, tablaPrimera);
                // Guardar resultados Primera Nacional
                GuardarResultados(conn, tx, temporadaId, 2, tablaB);
                // Marcar temporada como finalizada
                using var cmd = new NpgsqlCommand(
                    "UPDATE temporadas SET finalizada = true, activa = false, fecha_fin = NOW() WHERE id = @Id", conn, tx);
                cmd.Parameters.AddWithValue("@Id", temporadaId);
                cmd.ExecuteNonQuery();
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

                // Registrar títulos en el palmarés
                var temporadaNombre = GetTodasLasTemporadas().FirstOrDefault(t => t.Id == temporadaId)?.Nombre ?? $"Temporada {temporadaId}";

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
            List<(string nombre, int divisionId)> equiposNuevos)
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
                    "INSERT INTO temporadas (numero, nombre, fecha_inicio, activa, finalizada) VALUES (@N, @Nom, NOW(), true, false) RETURNING id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@N",   numero);
                    cmd.Parameters.AddWithValue("@Nom", nombre);
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
                    "INSERT INTO fechas (divisionid, numero, nombre, activa, habilitada) VALUES (@D, @N, @Nom, true, false) RETURNING id", conn, tx);
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

                int total = equiposPrimera.Count + equiposB.Count; // total de equipos

                // Calcular la potencia de 2 más cercana POR ARRIBA al total
                // que será el tamaño de la ronda principal (16, 8, 4, 2)
                int tamañoBracket = 2;
                while (tamañoBracket < total) tamañoBracket *= 2;
                // Si el total ya es justo una potencia de 2 menor permitida, usarla
                // (ej: si total=10, tamañoBracket=16; si total=8, tamañoBracket=8)

                // Si tamañoBracket == total, no hay fase previa
                int equiposEnPrevia = 0;
                int avanzanDirecto;

                if (tamañoBracket == total)
                {
                    avanzanDirecto = total;
                }
                else
                {
                    // Necesitamos reducir 'total' a 'tamañoBracket' (la potencia de 2 inferior)
                    // tamañoBracketInferior = tamañoBracket / 2
                    int tamañoInferior = tamañoBracket / 2;
                    // sobran = total - tamañoInferior equipos que deben jugar fase previa
                    // de los cuales la mitad gana y se suma a los que ya pasan directo
                    int sobran = total - tamañoInferior;
                    // sobran equipos juegan entre sí: sobran/2 partidos -> sobran/2 ganadores
                    // Pero sobran puede ser impar; en ese caso ajustamos
                    if (sobran % 2 != 0) sobran++; // forzar par (uno deberá pasar directo "gratis")
                    equiposEnPrevia = sobran;
                    int gPreviaTmp = equiposEnPrevia / 2;
                    avanzanDirecto = total - equiposEnPrevia + gPreviaTmp;
                    // avanzanDirecto debería ser igual a tamañoInferior, pero puede ser tamañoInferior
                    // Ajustamos tamañoBracket a tamañoInferior * 2 si corresponde, o usamos el inferior directamente
                    tamañoBracket = avanzanDirecto <= tamañoInferior ? tamañoInferior :
                                    (avanzanDirecto <= tamañoInferior * 2 ? tamañoInferior * 2 : tamañoBracket);
                    // Simplificación: la ronda principal tiene 'avanzanDirecto' partidos/2... 
                    // En la práctica, fijamos tamañoBracket = siguiente potencia de 2 >= avanzanDirecto
                    tamañoBracket = 2;
                    while (tamañoBracket < avanzanDirecto) tamañoBracket *= 2;
                }

                int nP = equiposPrimera.Count;
                int nB = equiposB.Count;

                // Separar B: nuevos primero para fase previa, luego peores veteranos
                var nuevos    = (equiposNuevosB ?? new List<int>()).Where(id => equiposB.Contains(id)).ToList();
                var veteranos = equiposB.Except(nuevos).ToList();

                // Armar lista de TODOS los equipos para decidir fase previa
                // Priorizamos que los de Primera NUNCA jueguen fase previa si hay suficientes de B
                var paraPrevia = new List<int>();
                if (equiposEnPrevia > 0)
                {
                    paraPrevia.AddRange(nuevos.Take(equiposEnPrevia));
                    if (paraPrevia.Count < equiposEnPrevia)
                        paraPrevia.AddRange(veteranos.TakeLast(equiposEnPrevia - paraPrevia.Count));
                    // Si aún falta (B muy chico), completar con los últimos de Primera
                    if (paraPrevia.Count < equiposEnPrevia)
                    {
                        var faltan = equiposEnPrevia - paraPrevia.Count;
                        paraPrevia.AddRange(equiposPrimera.TakeLast(faltan));
                    }
                    paraPrevia = paraPrevia.Take(equiposEnPrevia).OrderBy(_ => Guid.NewGuid()).ToList();
                }

                // Equipos que pasan directo a la ronda principal (todos menos los de previa)
                var directos = equiposPrimera.Concat(equiposB).Except(paraPrevia).OrderBy(_ => Guid.NewGuid()).ToList();
                var primeraDirectos = directos.Where(id => equiposPrimera.Contains(id)).ToList();
                var bDirectos       = directos.Where(id => equiposB.Contains(id)).ToList();

                int ganadoresPrevia = equiposEnPrevia / 2;

                // ── NOMBRE de la ronda principal según tamañoBracket ──
                string ronPrincipal = tamañoBracket switch {
                    >= 16 => "Dieciseisavos de Final",
                    8     => "Octavos de Final",
                    4     => "Cuartos de Final",
                    2     => "Semifinales",
                    _     => "Final"
                };

                // ── Construir lista de rondas EN ORDEN ──
                var secuencia = new[] { "Dieciseisavos de Final", "Octavos de Final", "Cuartos de Final", "Semifinales", "Final" };
                var rondas = new List<string>();
                if (equiposEnPrevia > 0) rondas.Add("Fase Previa");
                bool agregar = false;
                foreach (var r in secuencia)
                {
                    if (r == ronPrincipal) agregar = true;
                    if (agregar) rondas.Add(r);
                }
                if (!rondas.Contains("Final")) rondas.Add("Final");

                // Insertar rondas
                var rondaIds = new Dictionary<string, int>();
                for (int i = 0; i < rondas.Count; i++)
                {
                    using var cmd = new NpgsqlCommand(
                        "INSERT INTO copa_rondas (copa_id, nombre, orden, habilitada) VALUES (@C, @N, @O, @H) RETURNING id", conn, tx);
                    cmd.Parameters.AddWithValue("@C", copaId);
                    cmd.Parameters.AddWithValue("@N", rondas[i]);
                    cmd.Parameters.AddWithValue("@O", i + 1);
                    cmd.Parameters.AddWithValue("@H", i == 0);
                    rondaIds[rondas[i]] = Convert.ToInt32(cmd.ExecuteScalar());
                }

                // ── FASE PREVIA ──
                if (equiposEnPrevia > 0 && rondaIds.ContainsKey("Fase Previa"))
                {
                    for (int i = 0; i + 1 < paraPrevia.Count; i += 2)
                        InsertarPartidoCopa(conn, tx, rondaIds["Fase Previa"], copaId,
                            paraPrevia[i], paraPrevia[i + 1], i / 2);
                }

                // ── RONDA PRINCIPAL (tamañoBracket/2 partidos) ──
                int partidosPrincipal = tamañoBracket / 2;
                if (rondaIds.ContainsKey(ronPrincipal))
                {
                    int pos = 0;
                    // Cruces directos: Primera vs B (mientras haya de ambos)
                    int crucesDirectos = Math.Min(primeraDirectos.Count, bDirectos.Count);
                    for (int i = 0; i < crucesDirectos; i++)
                        InsertarPartidoCopa(conn, tx, rondaIds[ronPrincipal], copaId,
                            primeraDirectos[i], bDirectos[i], pos++);

                    // Equipos restantes (si Primera o B tienen de más) entre sí
                    var restantesPrimera = primeraDirectos.Skip(crucesDirectos).ToList();
                    var restantesB       = bDirectos.Skip(crucesDirectos).ToList();
                    var restantes = restantesPrimera.Concat(restantesB).ToList();
                    for (int i = 0; i + 1 < restantes.Count && pos < partidosPrincipal - ganadoresPrevia; i += 2)
                        InsertarPartidoCopa(conn, tx, rondaIds[ronPrincipal], copaId,
                            restantes[i], restantes[i+1], pos++);
                    // Si queda 1 equipo suelto, le damos un slot con ganador de previa
                    var sueltoIdx = restantes.Count % 2 == 1 ? restantes.Count - 1 : -1;

                    // Slots para ganadores de fase previa
                    for (int i = 0; i < ganadoresPrevia && pos < partidosPrincipal; i++)
                    {
                        if (sueltoIdx >= 0)
                        {
                            InsertarPartidoCopaConUnEquipo(conn, tx, rondaIds[ronPrincipal], copaId, restantes[sueltoIdx], pos++);
                            sueltoIdx = -1;
                        }
                        else
                        {
                            InsertarPartidoCopaVacio(conn, tx, rondaIds[ronPrincipal], copaId, pos++);
                        }
                    }

                    // Completar slots vacíos restantes si faltan
                    while (pos < partidosPrincipal)
                        InsertarPartidoCopaVacio(conn, tx, rondaIds[ronPrincipal], copaId, pos++);
                }

                // ── RONDAS SIGUIENTES vacías ──
                int partAnterior = partidosPrincipal;
                int idxPrincipal = rondas.IndexOf(ronPrincipal);
                for (int ri = idxPrincipal + 1; ri < rondas.Count; ri++)
                {
                    var ronNom = rondas[ri];
                    if (!rondaIds.ContainsKey(ronNom)) continue;
                    partAnterior = Math.Max(1, partAnterior / 2);
                    for (int i = 0; i < partAnterior; i++)
                        InsertarPartidoCopaVacio(conn, tx, rondaIds[ronNom], copaId, i);
                }

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
                using (var cmd = new NpgsqlCommand(@"
                    SELECT copa_id, ronda_id, posicion_bracket,
                           CASE WHEN goles_local > goles_visitante THEN equipo_local_id
                                ELSE equipo_visitante_id END as ganador_id
                    FROM copa_partidos WHERE id = @Id", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@Id", partidoId);
                    using var r = cmd.ExecuteReader();
                    if (!r.Read()) { tx.Rollback(); return false; }
                    copaId   = r.GetInt32(0);
                    rondaId  = r.GetInt32(1);
                    posicion = r.GetInt32(2);
                    ganadorId = r.IsDBNull(3) ? 0 : r.GetInt32(3);
                }

                // AUTO-AVANCE: buscar slot vacío en la siguiente ronda
                if (ganadorId > 0)
                {
                    int? siguienteRondaId = null;
                    using (var cmd2 = new NpgsqlCommand(@"
                        SELECT id FROM copa_rondas
                        WHERE copa_id = @C
                          AND orden > (SELECT orden FROM copa_rondas WHERE id = @R)
                        ORDER BY orden LIMIT 1", conn, tx))
                    {
                        cmd2.Parameters.AddWithValue("@C", copaId);
                        cmd2.Parameters.AddWithValue("@R", rondaId);
                        var res = cmd2.ExecuteScalar();
                        if (res != null) siguienteRondaId = Convert.ToInt32(res);
                    }

                    if (siguienteRondaId.HasValue)
                    {
                        // Buscar partido en siguiente ronda donde local o visitante esté vacío
                        // La posición del partido siguiente = posicion_actual / 2
                        int posSiguiente = posicion / 2;
                        bool esLocal = posicion % 2 == 0;

                        // Solo actualizar si ese campo está vacío (no pisar equipos ya asignados)
                        var campoCheck = esLocal ? "equipo_local_id" : "equipo_visitante_id";
                        using var cmdAdv = new NpgsqlCommand($@"
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
            cmd.Parameters.AddWithValue("@L", localId);
            cmd.Parameters.AddWithValue("@V", visitanteId);
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
            Activa = r.GetBoolean(5), Finalizada = r.GetBoolean(6)
        };

        private static Copa MapCopa(IDataReader r) => new()
        {
            Id = r.GetInt32(0), TemporadaId = r.IsDBNull(1) ? null : r.GetInt32(1),
            Tipo = r.GetString(2), Nombre = r.GetString(3), Finalizada = r.GetBoolean(4)
        };

        // ── PALMARÉS ───────────────────────────────────

        public void AgregarTitulo(int equipoId, string tipoTitulo, string nombreTitulo, int? temporadaId, string temporadaNombre)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(@"
                INSERT INTO palmares (equipo_id, tipo_titulo, nombre_titulo, temporada_id, temporada_nombre)
                VALUES (@E, @T, @N, @TId, @TNom)
                ON CONFLICT DO NOTHING", conn);
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
                    SELECT id, nombre_equipo, COALESCE(equipo_id,0), tipo_titulo, nombre_titulo, temporada_id, temporada_nombre
                    FROM palmares
                    WHERE tipo_titulo = @T
                    ORDER BY created_at DESC", conn);
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
