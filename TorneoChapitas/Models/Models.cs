using System.ComponentModel.DataAnnotations;

namespace TorneoAmigos.Models
{
    public class Division
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string? Descripcion { get; set; }
        public int Orden { get; set; }
        public bool Activa { get; set; } = true;
    }

    public class Equipo
    {
        public int Id { get; set; }
        public int DivisionId { get; set; }
        public string Nombre { get; set; } = "";
        public string? Escudo { get; set; }
        public string ColorPrincipal { get; set; } = "#003366";
        public string ColorSecundario { get; set; } = "#FFD700";
        public string FlagCode { get; set; } = "";
        public bool Activo { get; set; } = true;
    }

    public class Fecha
    {
        public int Id { get; set; }
        public int DivisionId { get; set; }
        public int Numero { get; set; }
        public string Nombre { get; set; } = "";
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public bool Activa { get; set; } = true;
    }

    public class Partido
    {
        public int Id { get; set; }
        public int FechaId { get; set; }
        public int DivisionId { get; set; }
        public int EquipoLocalId { get; set; }
        public int EquipoVisitanteId { get; set; }
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }
        public bool Jugado { get; set; } = false;
        public DateTime? FechaPartido { get; set; }
        public string? Lugar { get; set; }
        public string? Observaciones { get; set; }
        public Equipo? EquipoLocal { get; set; }
        public Equipo? EquipoVisitante { get; set; }
    }

    public class PosicionViewModel
    {
        public int Posicion { get; set; }
        public int EquipoId { get; set; }
        public string NombreEquipo { get; set; } = "";
        public string FlagCode { get; set; } = "";
        public string ColorPrincipal { get; set; } = "#003366";
        public int PartidosJugados { get; set; }
        public int Ganados { get; set; }
        public int Perdidos { get; set; }
        public int GolesAFavor { get; set; }
        public int GolesEnContra { get; set; }
        public int DiferenciaGoles => GolesAFavor - GolesEnContra;

        // Puntos acumulados directamente (no se multiplican después)
        public int Puntos { get; set; }

        public string? Zona { get; set; }
    }

    public class DivisionViewModel
    {
        public Division? Division { get; set; }
        public List<PosicionViewModel> TablaPosiciones { get; set; } = new();
        public List<GrupoFecha> Fixture { get; set; } = new();
        public List<Equipo> Equipos { get; set; } = new();
    }

    public class GrupoFecha
    {
        public Fecha Fecha { get; set; } = new();
        public List<Partido> Partidos { get; set; } = new();
    }

    public class HomeViewModel
    {
        public DivisionViewModel PrimeraDivision { get; set; } = new();
        public DivisionViewModel NacionalB { get; set; } = new();
        public int TotalPartidosJugados { get; set; }
        public int TotalGoles { get; set; }
    }

    public class CargarResultadoViewModel
    {
        public int PartidoId { get; set; }
        public string EquipoLocal { get; set; } = "";
        public string FlagLocal { get; set; } = "";
        public string EquipoVisitante { get; set; } = "";
        public string FlagVisitante { get; set; } = "";

        [Required][Range(0, 99)]
        public int GolesLocal { get; set; }

        [Required][Range(0, 99)]
        public int GolesVisitante { get; set; }

        public string? Observaciones { get; set; }
    }

    // ── COPAS ──────────────────────────────────────
    public class BracketTeam
    {
        public string Nombre { get; set; } = "";
        public string FlagCode { get; set; } = "";
        public int? Goles { get; set; }
        public bool Ganador { get; set; }
        public bool TBD { get; set; } = false;
    }

    public class BracketMatch
    {
        public int Id { get; set; }
        public BracketTeam Local { get; set; } = new();
        public BracketTeam Visitante { get; set; } = new();
        public bool Jugado { get; set; }
    }

    public class BracketRound
    {
        public string Nombre { get; set; } = "";
        public List<BracketMatch> Partidos { get; set; } = new();
    }

    public class CopaViewModel
    {
        public string NombreCopa { get; set; } = "";
        public string Icono { get; set; } = "🏆";
        public List<BracketRound> Rondas { get; set; } = new();
    }
}
