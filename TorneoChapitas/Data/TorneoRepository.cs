using System.Data;
using Npgsql;
using TorneoAmigos.Models;

namespace TorneoAmigos.Data
{
    public static class BanderaMap
    {
        private static readonly Dictionary<string, string> _codes = new()
        {
            {"Pato","nl"},{"Ponti","dk"},{"Juani","cr"},{"Tiago RC","it"},
            {"Nahuel","us"},{"Nacho G","ir"},{"Matic","gb-wls"},{"Lucas M","be"},
            {"Tiago S","ua"},{"Enzo","de"},
            {"Joan","kr"},{"Pocho","ec"},{"Jere","br"},{"Fede O","es"},
            {"Tomás","vn"},{"Carlos","co"},{"Sebas C","ar"},{"Santino","ch"},{"Lucas G","ph"}
        };
        public static string GetCode(string nombre) =>
            _codes.TryGetValue(nombre, out var c) ? c : "";
    }

    public class TorneoRepository
    {
        private readonly string _connectionString;

        public TorneoRepository(IConfiguration cfg) =>
            _connectionString = cfg.GetConnectionString("TorneoAmigosDB")
                ?? throw new InvalidOperationException("Connection string not found.");

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
        public List<Equipo> GetEquiposByDivision(int divisionId)
        {
            var lista = new List<Equipo>();
            const string sql = @"SELECT id, divisionid, nombre, escudo, colorprincipal, colorsecundario, activo
                                 FROM equipos WHERE divisionid = @D AND activo = true ORDER BY nombre";
            using var conn = GetConnection();
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@D", divisionId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(MapEquipo(r));
            return lista;
        }

        // ── TABLA DE POSICIONES ─────────────────
        public List<PosicionViewModel> GetTablaPosiciones(int divisionId)
        {
            var partidos = GetPartidosJugados(divisionId);
            var equipos  = GetEquiposByDivision(divisionId);

            var tabla = equipos.Select(e => new PosicionViewModel
            {
                EquipoId       = e.Id,
                NombreEquipo   = e.Nombre,
                FlagCode       = BanderaMap.GetCode(e.Nombre),
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

            bool esPrimera = divisionId == 1;
            for (int i = 0; i < ordenada.Count; i++)
            {
                ordenada[i].Posicion = i + 1;
                if (esPrimera)
                    ordenada[i].Zona = i == 0 ? "campeon" : i >= ordenada.Count - 2 ? "descenso" : "";
                else
                    ordenada[i].Zona = i < 2 ? "ascenso" : "";
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
                       el.nombre, el.colorprincipal, ev.nombre, ev.colorprincipal
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
            GetTodosLosPartidos(divisionId).Where(p => p.Jugado).ToList();

        public Partido? GetPartidoById(int id)
        {
            const string sql = @"
                SELECT p.id, p.fechaid, p.divisionid, p.equipolocalid, p.equipovisitanteid,
                       p.goleslocal, p.golesvisitante, p.jugado, p.fechapartido, p.lugar, p.observaciones,
                       el.nombre, el.colorprincipal, ev.nombre, ev.colorprincipal
                FROM partidos p
                INNER JOIN equipos el ON p.equipolocalid    = el.id
                INNER JOIN equipos ev ON p.equipovisitanteid = ev.id
                WHERE p.id = @Id";
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            using var r = cmd.ExecuteReader();
            return r.Read() ? MapPartido(r) : null;
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
            const string sql = @"SELECT id, divisionid, numero, nombre, fechainicio, fechafin, activa
                                 FROM fechas WHERE divisionid = @D ORDER BY numero";
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
            FlagCode = BanderaMap.GetCode(r.GetString(2)),
            Activo = r.GetBoolean(6)
        };

        private static Fecha MapFecha(IDataReader r) => new()
        {
            Id = r.GetInt32(0), DivisionId = r.GetInt32(1), Numero = r.GetInt32(2),
            Nombre = r.GetString(3),
            FechaInicio = r.IsDBNull(4) ? null : r.GetDateTime(4),
            FechaFin    = r.IsDBNull(5) ? null : r.GetDateTime(5),
            Activa = r.GetBoolean(6)
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
            EquipoLocal = new Equipo
            {
                Id = r.GetInt32(3), Nombre = r.GetString(11),
                ColorPrincipal = r.IsDBNull(12) ? "#003366" : r.GetString(12),
                FlagCode = BanderaMap.GetCode(r.GetString(11))
            },
            EquipoVisitante = new Equipo
            {
                Id = r.GetInt32(4), Nombre = r.GetString(13),
                ColorPrincipal = r.IsDBNull(14) ? "#003366" : r.GetString(14),
                FlagCode = BanderaMap.GetCode(r.GetString(13))
            }
        };
    }
}
