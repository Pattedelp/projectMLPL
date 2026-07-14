using System.Data;
using Npgsql;
using TorneoAmigos.Models;

namespace TorneoAmigos.Data
{
    public static class BanderaMap
    {
        private static readonly Dictionary<string, string> _codes = new()
        {
            // Jugadores actuales
            {"Pato","nl"},{"Ponti","dk"},{"Juani","cr"},{"Tiago RC","it"},
            {"Nahuel","us"},{"Nacho G","ir"},{"Matic","gb-wls"},{"Lucas M","be"},
            {"Tiago S","ua"},{"Enzo","de"},
            {"Joan","kr"},{"Pocho","ec"},{"Jere","br"},{"Fede O","es"},
            {"Tomás","vn"},{"Carlos","co"},{"Sebas C","ar"},{"Santino","uy"},{"Lucas G","ph"},
            {"Milton","jp"},{"Pablo","pt"},{"Eze","sa"},{"Yornier","gb-eng"},
            {"Licha","tr"},{"Mathi R","ng"},
            /// Jugadores históricos activos
            {"Pato L","nl"},{"Nahuel G","us"},{"Juani S","cr"},{"Valen M","ma"},{"Gonza","se"},
            // Jugadores retirados
            {"Monti","retired"},{"Pipe","retired"},{"Nacho M","retired"},
            {"Juanma","retired"},{"Bauti","retired"},
            {"Juli V","retired"},{"Santi DM","retired"},{"Tomi V","retired"},
            {"Juanchi C","retired"},{"Nico M","retired"}
        };
        public static string GetCode(string nombre) =>
            _codes.TryGetValue(nombre, out var c) ? c : "";

        public static bool IsRetired(string nombre) =>
            _codes.TryGetValue(nombre, out var c) && c == "retired";
    }

    public class TorneoRepository
    {
        private readonly string _connectionString;
        private readonly TemporadaRepository? _tempRepo;

        public TorneoRepository(IConfiguration cfg, TemporadaRepository? tempRepo = null)
        {
            _connectionString = cfg.GetConnectionString("TorneoAmigosDB")
                ?? throw new InvalidOperationException("Connection string not found.");
            _tempRepo = tempRepo;
        }

        private NpgsqlConnection GetConnection() => new(_connectionString);

        // ── DIVISIONES ─────────────────────────
        public Division? GetDivisionById(int id)
        {
            const string sql = "SELECT id, nombre, descripcion, orden, activa FROM divisiones WHERE id = @Id";
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            using var r = cmd.ExecuteReader();
            return r.Read() ? MapDivision(r) : null;
        }

        // ── EQUIPOS ────────────────────────────
        public List<Equipo> GetEquiposRetirados()
        {
            var lista = new List<Equipo>();
            const string sql = @"SELECT id, divisionid, nombre, escudo, colorprincipal, colorsecundario, activo, COALESCE(pais_code,'') as pais_code
                                 FROM equipos WHERE activo = false ORDER BY nombre";
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(sql, conn);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(new Equipo
            {
                Id = r.GetInt32(0), DivisionId = r.GetInt32(1), Nombre = r.GetString(2),
                Escudo = r.IsDBNull(3) ? null : r.GetString(3),
                ColorPrincipal  = r.IsDBNull(4) ? "#003366" : r.GetString(4),
                ColorSecundario = r.IsDBNull(5) ? "#FFD700" : r.GetString(5),
                Activo   = false,
                FlagCode = !string.IsNullOrEmpty(r.GetString(7)) ? r.GetString(7) : BanderaMap.GetCode(r.GetString(2))
            });
            return lista;
        }

        public string GetTipoPartido(int partidoId)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(
                "SELECT COALESCE(tipo_partido,'regular') FROM partidos WHERE id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", partidoId);
            conn.Open();
            return cmd.ExecuteScalar() as string ?? "regular";
        }

        public Partido? GetPartidoById(int partidoId)
        {
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(@"
                SELECT p.id, p.fechaid, p.divisionid, p.equipolocalid, p.equipovisitanteid,
                       p.goleslocal, p.golesvisitante, p.jugado, p.fechapartido, p.lugar, p.observaciones,
                       el.nombre, el.colorprincipal, ev.nombre, ev.colorprincipal,
                       COALESCE(el.pais_code,''), COALESCE(ev.pais_code,''),
                       COALESCE(p.tipo_partido,'regular')
                FROM partidos p
                INNER JOIN equipos el ON p.equipolocalid = el.id
                INNER JOIN equipos ev ON p.equipovisitanteid = ev.id
                WHERE p.id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", partidoId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            return r.Read() ? MapPartido(r) : null;
        }

        public void SetJugadorActivo(int id, bool activo)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand("UPDATE equipos SET activo = @A WHERE id = @Id", conn);
            cmd.Parameters.AddWithValue("@A",  activo);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            cmd.ExecuteNonQuery();
        }

        public List<Equipo> GetEquiposByDivision(int divisionId)
        {
            var lista = new List<Equipo>();
            const string sql = @"SELECT id, divisionid, nombre, escudo, colorprincipal, colorsecundario, activo, COALESCE(pais_code,'') as pais_code
                                 FROM equipos WHERE divisionid = @D AND activo = true ORDER BY nombre";
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@D", divisionId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(MapEquipo(r));
            return lista;
        }

        private List<Equipo> GetEquiposByDivisionIncluyendoInactivos(int divisionId)
        {
            var lista = new List<Equipo>();
            const string sql = @"SELECT id, divisionid, nombre, escudo, colorprincipal, colorsecundario, activo, COALESCE(pais_code,'') as pais_code
                                 FROM equipos WHERE divisionid = @D ORDER BY nombre";
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@D", divisionId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(MapEquipo(r));
            return lista;
        }

        // ── EQUIPO INDIVIDUAL ────────────────────
        public Equipo? GetEquipoById(int id)
        {
            const string sql = @"SELECT id, nombre, colorprincipal, colorsecundario, divisionid,
                                         COALESCE(pais_code,''), COALESCE(descripcion,'')
                                  FROM equipos WHERE id = @Id";
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            var nombre   = r.GetString(1);
            var paisCode = r.GetString(5);
            return new Equipo
            {
                Id              = r.GetInt32(0),
                Nombre          = nombre,
                ColorPrincipal  = r.GetString(2),
                ColorSecundario = r.GetString(3),
                DivisionId      = r.GetInt32(4),
                FlagCode        = !string.IsNullOrEmpty(paisCode) ? paisCode : BanderaMap.GetCode(nombre),
                Descripcion     = string.IsNullOrEmpty(r.GetString(6)) ? null : r.GetString(6)
            };
        }

        public bool ActualizarDescripcionEquipo(int id, string descripcion)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand("UPDATE equipos SET descripcion = @D WHERE id = @Id", conn);
            cmd.Parameters.AddWithValue("@D",  descripcion);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        // ── HEAD TO HEAD ─────────────────────────
        public HeadToHeadViewModel? GetHeadToHead(int equipoAId, int equipoBId)
        {
            var equipoA = GetEquipoById(equipoAId);
            var equipoB = GetEquipoById(equipoBId);
            if (equipoA == null || equipoB == null) return null;

            var vm = new HeadToHeadViewModel { EquipoA = equipoA, EquipoB = equipoB };

            const string sql = @"
                SELECT goleslocal, golesvisitante, equipolocalid, equipovisitanteid, temporada_nombre FROM (
                    SELECT p.goleslocal, p.golesvisitante, p.equipolocalid, p.equipovisitanteid,
                           'Temporada actual' as temporada_nombre, p.id as ord
                    FROM partidos p
                    WHERE p.jugado = true
                      AND ((p.equipolocalid = @A AND p.equipovisitanteid = @B)
                        OR (p.equipolocalid = @B AND p.equipovisitanteid = @A))

                    UNION ALL

                    SELECT cp.goles_local, cp.goles_visitante, cp.equipo_local_id, cp.equipo_visitante_id,
                           CONCAT(c.nombre, ' - ', cr.nombre) as temporada_nombre, -cp.id as ord
                    FROM copa_partidos cp
                    JOIN copa_rondas cr ON cp.ronda_id = cr.id
                    JOIN copas c ON cp.copa_id = c.id
                    WHERE cp.jugado = true
                      AND ((cp.equipo_local_id = @A AND cp.equipo_visitante_id = @B)
                        OR (cp.equipo_local_id = @B AND cp.equipo_visitante_id = @A))

                    UNION ALL

                    SELECT eh.goles_local, eh.goles_visitante, eh.equipo_local_id, eh.equipo_visitante_id,
                           COALESCE(eh.temporada_nombre, 'Histórico') as temporada_nombre, -1000000 - eh.id as ord
                    FROM enfrentamientos_historicos eh
                    WHERE (eh.equipo_local_id = @A AND eh.equipo_visitante_id = @B)
                       OR (eh.equipo_local_id = @B AND eh.equipo_visitante_id = @A)
                ) t
                ORDER BY ord DESC";

            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@A", equipoAId);
            cmd.Parameters.AddWithValue("@B", equipoBId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var gl = r.GetInt32(0);
                var gv = r.GetInt32(1);
                var localId = r.GetInt32(2);
                bool aEsLocal = localId == equipoAId;

                int golesA = aEsLocal ? gl : gv;
                int golesB = aEsLocal ? gv : gl;

                vm.Enfrentamientos.Add(new EnfrentamientoDirecto
                {
                    GolesA = golesA,
                    GolesB = golesB,
                    ALocal = aEsLocal,
                    TemporadaNombre = r.GetString(4)
                });

                if (golesA > golesB) vm.VictoriasA++;
                else if (golesB > golesA) vm.VictoriasB++;
                else vm.Empates++;
            }

            // Stats generales de cada equipo (de su división actual)
            var tablaA = GetTablaPosiciones(equipoA.DivisionId);
            var tablaB = GetTablaPosiciones(equipoB.DivisionId);
            vm.StatsA = tablaA.FirstOrDefault(t => t.EquipoId == equipoAId);
            vm.StatsB = tablaB.FirstOrDefault(t => t.EquipoId == equipoBId);

            return vm;
        }

        // ── TABLA DE POSICIONES ─────────────────
        public List<PosicionViewModel> GetTablaPosiciones(int divisionId)
        {
            var partidos = GetPartidosJugados(divisionId);
            var equipos  = GetEquiposByDivision(divisionId);

            // IDs de equipos que tienen al menos un partido en el fixture
            var equiposConPartido = new HashSet<int>(
                partidos.SelectMany(p => new[] { p.EquipoLocalId, p.EquipoVisitanteId })
                        .Where(id => id > 0));

            // Incluir también equipos inactivos que hayan jugado (ej: retirados durante la temporada)
            var idsActivos = new HashSet<int>(equipos.Select(e => e.Id));
            var idsFaltantes = equiposConPartido.Where(id => !idsActivos.Contains(id)).ToList();
            if (idsFaltantes.Any())
            {
                var equiposInactivos = GetEquiposByDivisionIncluyendoInactivos(divisionId);
                foreach (var e in equiposInactivos.Where(e => idsFaltantes.Contains(e.Id)))
                    equipos.Add(e);
            }

            // Solo mostrar equipos que participaron en el torneo
            var equiposFiltrados = equipos
                .Where(e => equiposConPartido.Contains(e.Id))
                .ToList();

            var tabla = equiposFiltrados.Select(e => new PosicionViewModel
            {
                EquipoId       = e.Id,
                NombreEquipo   = e.Nombre,
                FlagCode       = e.FlagCode, // ya viene de BD o BanderaMap desde GetEquiposByDivision
                ColorPrincipal = e.ColorPrincipal
            }).ToList();

            foreach (var p in partidos)
            {
                var loc = tabla.FirstOrDefault(t => t.EquipoId == p.EquipoLocalId);
                var vis = tabla.FirstOrDefault(t => t.EquipoId == p.EquipoVisitanteId);
                if (loc == null || vis == null) continue;

                loc.PartidosJugados++;
                vis.PartidosJugados++;
                loc.GolesAFavor   += p.GolesLocal ?? 0;
                loc.GolesEnContra += p.GolesVisitante ?? 0;
                vis.GolesAFavor   += p.GolesVisitante ?? 0;
                vis.GolesEnContra += p.GolesLocal ?? 0;

                int gl = p.GolesLocal ?? 0;
                int gv = p.GolesVisitante ?? 0;

                if (gl > gv)
                {
                    bool ajustado = (gl == 5 && gv == 4);
                    loc.Puntos += ajustado ? 2 : 3;
                    vis.Puntos += ajustado ? 1 : 0;
                    loc.Ganados++;
                    vis.Perdidos++;
                }
                else if (gv > gl)
                {
                    bool ajustado = (gv == 5 && gl == 4);
                    vis.Puntos += ajustado ? 2 : 3;
                    loc.Puntos += ajustado ? 1 : 0;
                    vis.Ganados++;
                    loc.Perdidos++;
                }
            }

            var ordenada = tabla
                .OrderByDescending(t => t.Puntos)
                .ThenByDescending(t => t.DiferenciaGoles)
                .ThenByDescending(t => t.GolesAFavor)
                .ThenBy(t => t.NombreEquipo)
                .ToList();

            // Calcular forma (últimos 5 partidos por equipo)
            var partidosPorEquipo = new Dictionary<int, List<string>>();
            foreach (var p in partidos.OrderBy(x => x.Id)) // cronológico
            {
                int gl = p.GolesLocal ?? 0;
                int gv = p.GolesVisitante ?? 0;
                int diff = Math.Abs(gl - gv);
                string resLocal = (gl > gv ? "V" : "D") + (diff == 1 ? "C" : "");
                string resVisit = (gv > gl ? "V" : "D") + (diff == 1 ? "C" : "");

                if (!partidosPorEquipo.ContainsKey(p.EquipoLocalId))    partidosPorEquipo[p.EquipoLocalId]    = new();
                if (!partidosPorEquipo.ContainsKey(p.EquipoVisitanteId)) partidosPorEquipo[p.EquipoVisitanteId] = new();
                partidosPorEquipo[p.EquipoLocalId].Add(resLocal);
                partidosPorEquipo[p.EquipoVisitanteId].Add(resVisit);
            }
            foreach (var eq in ordenada)
            {
                if (partidosPorEquipo.TryGetValue(eq.EquipoId, out var resultados))
                    eq.Forma = resultados.TakeLast(5).ToList();
            }

            bool esPrimera = divisionId == 1;

            // Obtener configuración de la temporada activa
            var tempActiva = _tempRepo?.GetTemporadaActiva();
            int cantDescensos   = tempActiva?.CantDescensos   ?? 2;
            int cantAscensos    = tempActiva?.CantAscensos    ?? 2;
            bool tienePromocion = tempActiva?.TienePromocion  ?? false;
            int posPromPrimera  = tempActiva?.PosPromocionPrimera ?? 8;
            int posPromB        = tempActiva?.PosPromocionB       ?? 3;

            for (int i = 0; i < ordenada.Count; i++)
            {
                ordenada[i].Posicion = i + 1;
                int pos1 = i + 1; // posición 1-based

                if (esPrimera)
                {
                    if (pos1 == 1)
                        ordenada[i].Zona = "campeon";
                    else if (tienePromocion && pos1 == posPromPrimera)
                        ordenada[i].Zona = "promocion";
                    else if (cantDescensos > 0 && pos1 > ordenada.Count - cantDescensos)
                        ordenada[i].Zona = "descenso";
                    else
                        ordenada[i].Zona = "";
                }
                else if (divisionId == 3) // Primera C — solo ascensos, sin promoción
                {
                    if (pos1 <= 2)
                        ordenada[i].Zona = "ascenso";
                    else
                        ordenada[i].Zona = "";
                }
                else // Nacional B
                {
                    if (pos1 <= cantAscensos)
                        ordenada[i].Zona = "ascenso";
                    else if (pos1 >= 3 && pos1 <= 6)  // reducido: 3° al 6°
                        ordenada[i].Zona = "promocion-b";
                    else if (pos1 > ordenada.Count - 2)  // últimos 2 descienden a C
                        ordenada[i].Zona = "descenso-c";
                    else
                        ordenada[i].Zona = "";
                }
            }
            return ordenada;
        }

        // ── FIXTURE ────────────────────────────
        public List<GrupoFecha> GetFixture(int divisionId)
        {
            var partidos = GetTodosLosPartidos(divisionId);
            var fechas   = GetFechasByDivision(divisionId);
            return fechas.Select(f => new GrupoFecha
            {
                Fecha    = f,
                Partidos = partidos.Where(p => p.FechaId == f.Id).ToList()
            }).Where(g => g.Partidos.Any()).ToList();
        }

        public List<Partido> GetTodosLosPartidos(int divisionId)
        {
            var lista = new List<Partido>();
            const string sql = @"
                SELECT p.id, p.fechaid, p.divisionid, p.equipolocalid, p.equipovisitanteid,
                       p.goleslocal, p.golesvisitante, p.jugado, p.fechapartido, p.lugar, p.observaciones,
                       el.nombre, el.colorprincipal, ev.nombre, ev.colorprincipal,
                       COALESCE(el.pais_code,''), COALESCE(ev.pais_code,''),
                       COALESCE(p.tipo_partido, 'regular')
                FROM partidos p
                INNER JOIN equipos el ON p.equipolocalid    = el.id
                INNER JOIN equipos ev ON p.equipovisitanteid = ev.id
                WHERE p.divisionid = @D ORDER BY p.fechaid, p.id";
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@D", divisionId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(MapPartido(r));
            return lista;
        }

        public List<Partido> GetPartidosJugados(int divisionId) =>
            GetTodosLosPartidos(divisionId)
                .Where(p => p.Jugado && p.TipoPartido == "regular")
                .ToList();

        public int GetDivisionIdDePartido(int partidoId)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand("SELECT divisionid FROM partidos WHERE id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", partidoId);
            conn.Open();
            var result = cmd.ExecuteScalar();
            return result == null ? 0 : Convert.ToInt32(result);
        }

        public bool CargarResultado(int partidoId, int golesLocal, int golesVisitante, string? obs = null)
        {
            const string sql = @"UPDATE partidos SET goleslocal=@GL, golesvisitante=@GV, jugado=true, observaciones=@Obs WHERE id=@Id";
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id",  partidoId);
            cmd.Parameters.AddWithValue("@GL",  golesLocal);
            cmd.Parameters.AddWithValue("@GV",  golesVisitante);
            cmd.Parameters.AddWithValue("@Obs", (object?)obs ?? DBNull.Value);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        // ── FECHAS ─────────────────────────────
        public List<Fecha> GetFechasByDivision(int divisionId)
        {
            var lista = new List<Fecha>();
            const string sql = @"SELECT f.id, f.divisionid, f.numero, f.nombre, f.fechainicio, f.fechafin, f.activa, f.habilitada
                                 FROM fechas f
                                 WHERE f.divisionid = @D
                                 AND (f.temporada_id IS NULL OR f.temporada_id = (SELECT id FROM temporadas WHERE activa = true ORDER BY id DESC FETCH FIRST 1 ROW ONLY))
                                 ORDER BY f.numero";
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@D", divisionId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(MapFecha(r));
            return lista;
        }

        // ── STATS ──────────────────────────────
        public int GetTotalGoles()
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand("SELECT COALESCE(SUM(goleslocal + golesvisitante), 0) FROM partidos WHERE jugado = true", conn);
            conn.Open();
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public int GetTotalPartidosJugados()
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand("SELECT COUNT(*) FROM partidos WHERE jugado = true", conn);
            conn.Open();
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        // ── ADMIN ──────────────────────────────
        public bool ToggleFechaHabilitada(int fechaId, bool habilitada)
        {
            const string sql = "UPDATE fechas SET habilitada = @H WHERE id = @Id";
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@H",   habilitada);
            cmd.Parameters.AddWithValue("@Id",  fechaId);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        public List<Fecha> GetTodasLasFechas(int divisionId)
        {
            var lista = new List<Fecha>();
            const string sql = @"SELECT id, divisionid, numero, nombre, fechainicio, fechafin, activa, habilitada
                                 FROM fechas WHERE divisionid = @D ORDER BY numero";
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@D", divisionId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(MapFecha(r));
            return lista;
        }

        // ── MAPPERS ────────────────────────────
        private static Division MapDivision(IDataReader r) => new()
        {
            Id = r.GetInt32(0), Nombre = r.GetString(1),
            Descripcion = r.IsDBNull(2) ? null : r.GetString(2),
            Orden = r.GetInt32(3), Activa = r.GetBoolean(4)
        };

        private static Equipo MapEquipo(IDataReader r) => new()
        {
            Id = r.GetInt32(0), DivisionId = r.GetInt32(1), Nombre = r.GetString(2),
            Escudo = r.IsDBNull(3) ? null : r.GetString(3),
            ColorPrincipal  = r.IsDBNull(4) ? "#003366" : r.GetString(4),
            ColorSecundario = r.IsDBNull(5) ? "#FFD700" : r.GetString(5),
            FlagCode = !string.IsNullOrEmpty(r.IsDBNull(7) ? "" : r.GetString(7))
                ? r.GetString(7)
                : BanderaMap.GetCode(r.GetString(2)),
            Activo   = r.GetBoolean(6)
        };

        private static Fecha MapFecha(IDataReader r) => new()
        {
            Id = r.GetInt32(0), DivisionId = r.GetInt32(1), Numero = r.GetInt32(2),
            Nombre = r.GetString(3),
            FechaInicio = r.IsDBNull(4) ? null : r.GetDateTime(4),
            FechaFin    = r.IsDBNull(5) ? null : r.GetDateTime(5),
            Activa = r.GetBoolean(6),
            Habilitada = r.FieldCount > 7 && !r.IsDBNull(7) && r.GetBoolean(7)
        };

        private static Partido MapPartido(IDataReader r) => new()
        {
            Id = r.GetInt32(0), FechaId = r.GetInt32(1), DivisionId = r.GetInt32(2),
            EquipoLocalId = r.GetInt32(3), EquipoVisitanteId = r.GetInt32(4),
            GolesLocal     = r.IsDBNull(5) ? null : r.GetInt32(5),
            GolesVisitante = r.IsDBNull(6) ? null : r.GetInt32(6),
            Jugado         = r.GetBoolean(7),
            FechaPartido   = r.IsDBNull(8) ? null : r.GetDateTime(8),
            Lugar          = r.IsDBNull(9) ? null : r.GetString(9),
            Observaciones  = r.IsDBNull(10) ? null : r.GetString(10),
            TipoPartido    = r.FieldCount > 17 && !r.IsDBNull(17) ? r.GetString(17) : "regular",
            EquipoLocal = new Equipo
            {
                Id = r.GetInt32(3), Nombre = r.GetString(11),
                ColorPrincipal = r.IsDBNull(12) ? "#003366" : r.GetString(12),
                FlagCode = r.FieldCount > 15 && !r.IsDBNull(15) && !string.IsNullOrEmpty(r.GetString(15))
                    ? r.GetString(15)
                    : BanderaMap.GetCode(r.GetString(11))
            },
            EquipoVisitante = new Equipo
            {
                Id = r.GetInt32(4), Nombre = r.GetString(13),
                ColorPrincipal = r.IsDBNull(14) ? "#003366" : r.GetString(14),
                FlagCode = r.FieldCount > 16 && !r.IsDBNull(16) && !string.IsNullOrEmpty(r.GetString(16))
                    ? r.GetString(16)
                    : BanderaMap.GetCode(r.GetString(13))
            }
        };
    }
}
