namespace TorneoAmigos.Models
{
    public class Temporada
    {
        public int Id { get; set; }
        public int Numero { get; set; }
        public string Nombre { get; set; } = "";
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public bool Activa { get; set; }
        public bool Finalizada { get; set; }
    }

    public class TemporadaResultado
    {
        public int Id { get; set; }
        public int TemporadaId { get; set; }
        public int EquipoId { get; set; }
        public string NombreEquipo { get; set; } = "";
        public string FlagCode { get; set; } = "";
        public int DivisionId { get; set; }
        public string NombreDivision { get; set; } = "";
        public int Posicion { get; set; }
        public int Puntos { get; set; }
        public int PartidosJugados { get; set; }
        public int Ganados { get; set; }
        public int Perdidos { get; set; }
        public int GolesFavor { get; set; }
        public int GolesContra { get; set; }
        public bool Campeon { get; set; }
        public bool Ascendio { get; set; }
        public bool Descendio { get; set; }
    }

    public class HistorialViewModel
    {
        public List<Temporada> Temporadas { get; set; } = new();
        public Temporada? TemporadaActiva { get; set; }
    }

    public class TemporadaDetalleViewModel
    {
        public Temporada Temporada { get; set; } = new();
        public List<TemporadaResultado> ResultadosPrimera { get; set; } = new();
        public List<TemporadaResultado> ResultadosNacionalB { get; set; } = new();
    }

    // Panel nueva temporada
    public class NuevaTemporadaViewModel
    {
        public int NumeroTemporada { get; set; }
        public List<EquipoCheckbox> EquiposPrimera { get; set; } = new();
        public List<EquipoCheckbox> EquiposNacionalB { get; set; } = new();
    }

    public class EquipoCheckbox
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string FlagCode { get; set; } = "";
        public int DivisionId { get; set; }
        public bool Seleccionado { get; set; } = true;
        public bool EsNuevo { get; set; } = false;
    }

    // Copa
    public class Copa
    {
        public int Id { get; set; }
        public int? TemporadaId { get; set; }
        public string Tipo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public bool Finalizada { get; set; }
    }

    public class CopaRonda
    {
        public int Id { get; set; }
        public int CopaId { get; set; }
        public string Nombre { get; set; } = "";
        public int Orden { get; set; }
        public bool Habilitada { get; set; }
        public List<CopaPartido> Partidos { get; set; } = new();
    }

    public class CopaPartido
    {
        public int Id { get; set; }
        public int RondaId { get; set; }
        public int CopaId { get; set; }
        public int? EquipoLocalId { get; set; }
        public int? EquipoVisitanteId { get; set; }
        public string NombreLocal { get; set; } = "Por definir";
        public string FlagLocal { get; set; } = "";
        public string NombreVisitante { get; set; } = "Por definir";
        public string FlagVisitante { get; set; } = "";
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }
        public bool Jugado { get; set; }
        public int PosicionBracket { get; set; }
    }

    public class CopaFullViewModel
    {
        public Copa Copa { get; set; } = new();
        public List<CopaRonda> Rondas { get; set; } = new();
        public bool EsAdmin { get; set; }
    }
}
