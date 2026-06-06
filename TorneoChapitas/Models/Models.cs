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

    // ---- ViewModels ----

    public class PosicionViewModel
    {
        public int Posicion { get; set; }
        public int EquipoId { get; set; }
        public string NombreEquipo { get; set; } = "";
        public string ColorPrincipal { get; set; } = "#003366";
        public int PartidosJugados { get; set; }
        public int Ganados { get; set; }
        public int Empatados { get; set; }
        public int Perdidos { get; set; }
        public int GolesAFavor { get; set; }
        public int GolesEnContra { get; set; }
        public int DiferenciaGoles => GolesAFavor - GolesEnContra;
        public int Puntos => (Ganados * 3) + Empatados;
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
        public string EquipoVisitante { get; set; } = "";

        [Required(ErrorMessage = "Ingresá los goles del local")]
        [Range(0, 99)]
        public int GolesLocal { get; set; }

        [Required(ErrorMessage = "Ingresá los goles del visitante")]
        [Range(0, 99)]
        public int GolesVisitante { get; set; }

        public string? Observaciones { get; set; }
    }
}
