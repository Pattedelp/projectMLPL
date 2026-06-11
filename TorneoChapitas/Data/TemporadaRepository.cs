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
            const string sql = @"
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

        public bool FinalizarTemporada(int temporadaId, List<PosicionViewModel> tablaPrimera, List<PosicionViewModel> tablaB, bool sinDescensos = false)
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
                // Ascensos (siempre se aplican)
                if (tablaB.Count >= 2)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var eq = tablaB[i];
                        if (eq.EquipoId > 0)
                            EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 1 WHERE id = @Id", eq.EquipoId);
                    }
                }
                // Descensos (solo si NO es temporada sin descensos)
                if (!sinDescensos && tablaPrimera.Count >= 2)
                {
                    for (int i = tablaPrimera.Count - 2; i < tablaPrimera.Count; i++)
                    {
                        var eq = tablaPrimera[i];
                        if (eq.EquipoId > 0)
                            EjecutarUpdate(conn, tx, "UPDATE equipos SET divisionid = 2 WHERE id = @Id", eq.EquipoId);
                    }
                }

                // Registrar títulos en el palmarés
                var temporadaNombre = GetTodasLasTemporadas().FirstOrDefault(t => t.Id == temporadaId)?.Nombre ?? $"Temporada {temporadaId}";
                if (tablaPrimera.Any())
                {
                    var campeon = tablaPrimera.First();
                    AgregarTitulo(campeon.EquipoId, "campeon_torneo", "Campeón Primera División", temporadaId, temporadaNombre);
                }
                // Campeón de la B
                if (tablaB.Any())
                {
                    var campeonB = tablaB.First();
                    AgregarTitulo(campeonB.EquipoId, "campeon_primera_b", "Campeón Primera Nacional", temporadaId, temporadaNombre);
                }

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
            const string sql = @"
                SELECT cp.id, cp.ronda_id, cp.copa_id,
                       cp.equipo_local_id, COALESCE(el.nombre, 'Por definir'),
                       cp.equipo_visitante_id, COALESCE(ev.nombre, 'Por definir'),
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
                lista.Add(new CopaPartido
                {
                    Id = r.GetInt32(0), RondaId = r.GetInt32(1), CopaId = r.GetInt32(2),
                    EquipoLocalId     = r.IsDBNull(3) ? null : r.GetInt32(3),
                    NombreLocal       = r.GetString(4),
                    FlagLocal         = r.IsDBNull(3) ? "" : BanderaMap.GetCode(r.GetString(4)),
                    EquipoVisitanteId = r.IsDBNull(5) ? null : r.GetInt32(5),
                    NombreVisitante   = r.GetString(6),
                    FlagVisitante     = r.IsDBNull(5) ? "" : BanderaMap.GetCode(r.GetString(6)),
                    GolesLocal        = r.IsDBNull(7) ? null : r.GetInt32(7),
                    GolesVisitante    = r.IsDBNull(8) ? null : r.GetInt32(8),
                    Jugado            = r.GetBoolean(9),
                    PosicionBracket   = r.GetInt32(10)
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

                int nP      = equiposPrimera.Count;
                int nB      = equiposB.Count;
                int sobranB = Math.Max(0, nB - nP);

                // Calcular cuántos de B van a fase previa:
                // Necesitamos que de la B salgan exactamente nP para octavos.
                // ganadoresPrevia = mitad de los que juegan fase previa
                // bDirectos = nP - ganadoresPrevia
                // Entonces: equiposEnPrevia = nB - bDirectosNeeded
                // bDirectosNeeded = nP - ganadoresPrevia
                // ganadoresPrevia = equiposEnPrevia / 2
                // Resolviendo: equiposEnPrevia = nB - nP + equiposEnPrevia/2
                // → equiposEnPrevia/2 = nB - nP → equiposEnPrevia = 2*(nB-nP) = 2*sobranB
                int equiposEnPrevia  = sobranB * 2;  // con 14B,10P → 4*2=8? NO
                // Corrección: con 14B y 10P queremos 10 en octavos
                // bDirectos + ganadoresPrevia = nP
                // (nB - equiposEnPrevia) + equiposEnPrevia/2 = nP
                // nB - equiposEnPrevia/2 = nP
                // equiposEnPrevia/2 = nB - nP = sobranB
                // equiposEnPrevia = 2 * sobranB ... pero con 14-10=4 → 8 en previa → 4 ganadores → 6+4=10 ✓
                equiposEnPrevia = sobranB * 2; // 4*2=8 en previa, 4 ganadores, 6 directos + 4 = 10 ✓
                int bDirectosCount = nB - equiposEnPrevia; // 14-8=6

                // Separar B: nuevos primero para fase previa, luego peores veteranos
                var nuevos    = (equiposNuevosB ?? new List<int>()).Where(id => equiposB.Contains(id)).ToList();
                var veteranos = equiposB.Except(nuevos).ToList();

                // Armar fase previa con nuevos primero, luego peores veteranos
                var paraPrevia = new List<int>();
                paraPrevia.AddRange(nuevos.Take(equiposEnPrevia));
                if (paraPrevia.Count < equiposEnPrevia)
                    paraPrevia.AddRange(veteranos.TakeLast(equiposEnPrevia - paraPrevia.Count));
                paraPrevia = paraPrevia.Take(equiposEnPrevia).OrderBy(_ => Guid.NewGuid()).ToList();

                // B directos = los mejores que NO van a fase previa
                var bDirectos = equiposB.Except(paraPrevia)
                                        .Take(bDirectosCount)
                                        .OrderBy(_ => Guid.NewGuid()).ToList();
                var primera   = equiposPrimera.OrderBy(_ => Guid.NewGuid()).ToList();

                int ganadoresPrevia2 = equiposEnPrevia / 2; // 4 ganadores

                // ── RONDAS en orden correcto ──────────────────────
                // Determinar ronda principal según nP partidos
                string ronPrincipal = nP >= 16 ? "Ronda de 16"
                                    : nP >= 8  ? "Octavos de Final"
                                    : nP >= 4  ? "Cuartos de Final"
                                    : nP >= 2  ? "Semifinales"
                                    : "Final";

                // Construir rondas EN ORDEN: siempre de más grande a más chico
                var rondas = new List<string>();
                if (sobranB > 0) rondas.Add("Fase Previa");
                rondas.Add(ronPrincipal);

                // Agregar rondas siguientes en orden correcto
                var secuenciaRondas = new[] { "Octavos de Final", "Cuartos de Final", "Semifinales", "Final" };
                bool agregando = false;
                foreach (var r in secuenciaRondas)
                {
                    if (r == ronPrincipal) { agregando = true; continue; }
                    if (agregando && !rondas.Contains(r)) rondas.Add(r);
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

                // ── FASE PREVIA ───────────────────────────────────
                if (sobranB > 0 && rondaIds.ContainsKey("Fase Previa"))
                {
                    for (int i = 0; i + 1 < paraPrevia.Count; i += 2)
                        InsertarPartidoCopa(conn, tx, rondaIds["Fase Previa"], copaId,
                            paraPrevia[i], paraPrevia[i + 1], i / 2);
                }

                // ── RONDA PRINCIPAL ───────────────────────────────
                if (rondaIds.ContainsKey(ronPrincipal))
                {
                    int ganadoresPrevia = ganadoresPrevia2;
                    int directos        = nP - ganadoresPrevia;

                    // Cruces directos: primera[i] vs bDirectos[i]
                    for (int i = 0; i < directos; i++)
                        InsertarPartidoCopa(conn, tx, rondaIds[ronPrincipal], copaId,
                            primera[i], bDirectos[i], i);

                    // Slots para ganadores de fase previa (solo local conocido)
                    for (int i = 0; i < ganadoresPrevia; i++)
                        InsertarPartidoCopaConUnEquipo(conn, tx, rondaIds[ronPrincipal], copaId,
                            primera[directos + i], directos + i);
                }

                // ── RONDAS SIGUIENTES vacías ──────────────────────
                int partAnterior = nP;
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

                if (ganadorId > 0)
                {
                    // Buscar la siguiente ronda
                    int? siguienteRondaId = null;
                    using (var cmd = new NpgsqlCommand(@"
                        SELECT id FROM copa_rondas
                        WHERE copa_id = @C AND orden > (SELECT orden FROM copa_rondas WHERE id = @R)
                        ORDER BY orden LIMIT 1", conn, tx))
                    {
                        cmd.Parameters.AddWithValue("@C", copaId);
                        cmd.Parameters.AddWithValue("@R", rondaId);
                        var result = cmd.ExecuteScalar();
                        if (result != null) siguienteRondaId = Convert.ToInt32(result);
                    }

                    if (siguienteRondaId.HasValue)
                    {
                        // Posición en la siguiente ronda = posición actual / 2 (redondeado abajo)
                        int posiciónSiguiente = posicion / 2;
                        bool esLocal = posicion % 2 == 0; // par = local, impar = visitante

                        // Actualizar el partido correspondiente en la siguiente ronda
                        var campo = esLocal ? "equipo_local_id" : "equipo_visitante_id";
                        using var cmd = new NpgsqlCommand($@"
                            UPDATE copa_partidos
                            SET {campo} = @G
                            WHERE ronda_id = @SR AND posicion_bracket = @PS AND copa_id = @C", conn, tx);
                        cmd.Parameters.AddWithValue("@G",  ganadorId);
                        cmd.Parameters.AddWithValue("@SR", siguienteRondaId.Value);
                        cmd.Parameters.AddWithValue("@PS", posiciónSiguiente);
                        cmd.Parameters.AddWithValue("@C",  copaId);
                        cmd.ExecuteNonQuery();
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
                VALUES (@E, @T, @N, @TId, @TNom)", conn);
            cmd.Parameters.AddWithValue("@E",    equipoId);
            cmd.Parameters.AddWithValue("@T",    tipoTitulo);
            cmd.Parameters.AddWithValue("@N",    nombreTitulo);
            cmd.Parameters.AddWithValue("@TId",  (object?)temporadaId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TNom", temporadaNombre);
            conn.Open();
            cmd.ExecuteNonQuery();
        }

        public PalmaresViewModel GetPalmares()
        {
            var titulos = new List<TorneoAmigos.Models.Titulo>();
            // nombre_equipo puede venir directo de la tabla (histórico) o de JOIN con equipos
            const string sql = @"
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
