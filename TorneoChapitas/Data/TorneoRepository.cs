using System.Data;
using System.Data.SqlClient;
using TorneoAmigos.Models;

namespace TorneoAmigos.Data
{
    public class TorneoRepository
    {
        private readonly string _connectionString;

        public TorneoRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("TorneoAmigosDB")
                ?? throw new InvalidOperationException("Connection string 'TorneoAmigosDB' not found.");
        }

        private SqlConnection GetConnection() => new SqlConnection(_connectionString);

        // ── DIVISIONES ─────────────────────────────────────────────

        public Division? GetDivisionById(int id)
        {
            const string sql = "SELECT Id, Nombre, Descripcion, Orden, Activa FROM Divisiones WHERE Id = @Id";
            using var conn = GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            using var r = cmd.ExecuteReader();
            return r.Read() ? MapDivision(r) : null;
        }

        // ── EQUIPOS ────────────────────────────────────────────────

        public List<Equipo> GetEquiposByDivision(int divisionId)
        {
            var lista = new List<Equipo>();
            const string sql = @"SELECT Id, DivisionId, Nombre, Escudo, ColorPrincipal, ColorSecundario, Activo
                                 FROM Equipos WHERE DivisionId = @DivisionId AND Activo = 1 ORDER BY Nombre";
            using var conn = GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DivisionId", divisionId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(MapEquipo(r));
            return lista;
        }

        // ── TABLA DE POSICIONES ────────────────────────────────────

        public List<PosicionViewModel> GetTablaPosiciones(int divisionId)
        {
            var partidos = GetPartidosJugados(divisionId);
            var equipos  = GetEquiposByDivision(divisionId);

            var tabla = equipos.Select(e => new PosicionViewModel
            {
                EquipoId       = e.Id,
                NombreEquipo   = e.Nombre,
                ColorPrincipal = e.ColorPrincipal
            }).ToList();

            foreach (var p in partidos)
            {
                var loc = tabla.FirstOrDefault(t => t.EquipoId == p.EquipoLocalId);
                var vis = tabla.FirstOrDefault(t => t.EquipoId == p.EquipoVisitanteId);
                if (loc == null || vis == null) continue;

                loc.PartidosJugados++; vis.PartidosJugados++;
                loc.GolesAFavor    += p.GolesLocal ?? 0;
                loc.GolesEnContra  += p.GolesVisitante ?? 0;
                vis.GolesAFavor    += p.GolesVisitante ?? 0;
                vis.GolesEnContra  += p.GolesLocal ?? 0;

                if (p.GolesLocal > p.GolesVisitante)      { loc.Ganados++;  vis.Perdidos++; }
                else if (p.GolesLocal < p.GolesVisitante) { vis.Ganados++;  loc.Perdidos++; }
                else                                       { loc.Empatados++; vis.Empatados++; }
            }

            var ordenada = tabla
                .OrderByDescending(t => t.Puntos)
                .ThenByDescending(t => t.DiferenciaGoles)
                .ThenByDescending(t => t.GolesAFavor)
                .ThenBy(t => t.NombreEquipo)
                .ToList();

            for (int i = 0; i < ordenada.Count; i++)
            {
                ordenada[i].Posicion = i + 1;
                ordenada[i].Zona = i < 2 ? "campeonato" : i < 4 ? "promocion" : i >= ordenada.Count - 2 ? "descenso" : "";
            }
            return ordenada;
        }

        // ── FIXTURE ────────────────────────────────────────────────

        public List<GrupoFecha> GetFixture(int divisionId)
        {
            var partidos = GetTodosLosPartidos(divisionId);
            var fechas   = GetFechasByDivision(divisionId);
            return fechas
                .Select(f => new GrupoFecha { Fecha = f, Partidos = partidos.Where(p => p.FechaId == f.Id).ToList() })
                .Where(g => g.Partidos.Any())
                .ToList();
        }

        public List<Partido> GetTodosLosPartidos(int divisionId)
        {
            var lista = new List<Partido>();
            const string sql = @"
                SELECT p.Id, p.FechaId, p.DivisionId, p.EquipoLocalId, p.EquipoVisitanteId,
                       p.GolesLocal, p.GolesVisitante, p.Jugado, p.FechaPartido, p.Lugar, p.Observaciones,
                       el.Nombre, el.ColorPrincipal, ev.Nombre, ev.ColorPrincipal
                FROM Partidos p
                INNER JOIN Equipos el ON p.EquipoLocalId    = el.Id
                INNER JOIN Equipos ev ON p.EquipoVisitanteId = ev.Id
                WHERE p.DivisionId = @DivisionId
                ORDER BY p.FechaId, p.Id";
            using var conn = GetConnection();
            using var cmd  = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DivisionId", divisionId);
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
                SELECT p.Id, p.FechaId, p.DivisionId, p.EquipoLocalId, p.EquipoVisitanteId,
                       p.GolesLocal, p.GolesVisitante, p.Jugado, p.FechaPartido, p.Lugar, p.Observaciones,
                       el.Nombre, el.ColorPrincipal, ev.Nombre, ev.ColorPrincipal
                FROM Partidos p
                INNER JOIN Equipos el ON p.EquipoLocalId    = el.Id
                INNER JOIN Equipos ev ON p.EquipoVisitanteId = ev.Id
                WHERE p.Id = @Id";
            using var conn = GetConnection();
            using var cmd  = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            using var r = cmd.ExecuteReader();
            return r.Read() ? MapPartido(r) : null;
        }

        public bool CargarResultado(int partidoId, int golesLocal, int golesVisitante, string? obs = null)
        {
            const string sql = @"UPDATE Partidos SET GolesLocal=@GL, GolesVisitante=@GV, Jugado=1, Observaciones=@Obs WHERE Id=@Id";
            using var conn = GetConnection();
            using var cmd  = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id",  partidoId);
            cmd.Parameters.AddWithValue("@GL",  golesLocal);
            cmd.Parameters.AddWithValue("@GV",  golesVisitante);
            cmd.Parameters.AddWithValue("@Obs", (object?)obs ?? DBNull.Value);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        // ── FECHAS ─────────────────────────────────────────────────

        public List<Fecha> GetFechasByDivision(int divisionId)
        {
            var lista = new List<Fecha>();
            const string sql = "SELECT Id, DivisionId, Numero, Nombre, FechaInicio, FechaFin, Activa FROM Fechas WHERE DivisionId=@DivisionId ORDER BY Numero";
            using var conn = GetConnection();
            using var cmd  = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DivisionId", divisionId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(MapFecha(r));
            return lista;
        }

        // ── ESTADÍSTICAS ───────────────────────────────────────────

        public int GetTotalGoles()
        {
            using var conn = GetConnection();
            using var cmd  = new SqlCommand("SELECT ISNULL(SUM(GolesLocal+GolesVisitante),0) FROM Partidos WHERE Jugado=1", conn);
            conn.Open();
            return (int)cmd.ExecuteScalar()!;
        }

        public int GetTotalPartidosJugados()
        {
            using var conn = GetConnection();
            using var cmd  = new SqlCommand("SELECT COUNT(*) FROM Partidos WHERE Jugado=1", conn);
            conn.Open();
            return (int)cmd.ExecuteScalar()!;
        }

        // ── MAPPERS ────────────────────────────────────────────────

        private static Division MapDivision(SqlDataReader r) => new()
        {
            Id = r.GetInt32(0), Nombre = r.GetString(1),
            Descripcion = r.IsDBNull(2) ? null : r.GetString(2),
            Orden = r.GetInt32(3), Activa = r.GetBoolean(4)
        };

        private static Equipo MapEquipo(SqlDataReader r) => new()
        {
            Id = r.GetInt32(0), DivisionId = r.GetInt32(1), Nombre = r.GetString(2),
            Escudo = r.IsDBNull(3) ? null : r.GetString(3),
            ColorPrincipal  = r.IsDBNull(4) ? "#003366" : r.GetString(4),
            ColorSecundario = r.IsDBNull(5) ? "#FFD700" : r.GetString(5),
            Activo = r.GetBoolean(6)
        };

        private static Fecha MapFecha(SqlDataReader r) => new()
        {
            Id = r.GetInt32(0), DivisionId = r.GetInt32(1), Numero = r.GetInt32(2),
            Nombre = r.GetString(3),
            FechaInicio = r.IsDBNull(4) ? null : r.GetDateTime(4),
            FechaFin    = r.IsDBNull(5) ? null : r.GetDateTime(5),
            Activa = r.GetBoolean(6)
        };

        private static Partido MapPartido(SqlDataReader r) => new()
        {
            Id = r.GetInt32(0), FechaId = r.GetInt32(1), DivisionId = r.GetInt32(2),
            EquipoLocalId = r.GetInt32(3), EquipoVisitanteId = r.GetInt32(4),
            GolesLocal      = r.IsDBNull(5) ? null : r.GetInt32(5),
            GolesVisitante  = r.IsDBNull(6) ? null : r.GetInt32(6),
            Jugado          = r.GetBoolean(7),
            FechaPartido    = r.IsDBNull(8) ? null : r.GetDateTime(8),
            Lugar           = r.IsDBNull(9) ? null : r.GetString(9),
            Observaciones   = r.IsDBNull(10) ? null : r.GetString(10),
            EquipoLocal     = new Equipo { Id = r.GetInt32(3), Nombre = r.GetString(11), ColorPrincipal = r.IsDBNull(12) ? "#003366" : r.GetString(12) },
            EquipoVisitante = new Equipo { Id = r.GetInt32(4), Nombre = r.GetString(13), ColorPrincipal = r.IsDBNull(14) ? "#003366" : r.GetString(14) }
        };
    }
}
