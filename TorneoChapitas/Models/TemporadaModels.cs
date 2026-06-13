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

    // ── PALMARÉS ────────────────────────────────────

    public class Titulo
    {
        public int Id { get; set; }
        public int EquipoId { get; set; }
        public string NombreEquipo { get; set; } = "";
        public string FlagCode { get; set; } = "";
        public string TipoTitulo { get; set; } = "";
        public string NombreTitulo { get; set; } = "";
        public int? TemporadaId { get; set; }
        public string TemporadaNombre { get; set; } = "";
    }

    public class PalmaresEquipo
    {
        public int EquipoId { get; set; }
        public string NombreEquipo { get; set; } = "";
        public string FlagCode { get; set; } = "";
        public int TotalTitulos { get; set; }
        public int CampeonatosLiga { get; set; }
        public int CopaArgentina { get; set; }
        public int Supercopa { get; set; }
        public List<Titulo> Titulos { get; set; } = new();
    }

    public class PalmaresViewModel
    {
        public List<PalmaresEquipo> Equipos { get; set; } = new();
        public List<Titulo> UltimosTitulos { get; set; } = new();
    }

    public class EstadisticasViewModel
    {
        public PalmaresViewModel Palmares { get; set; } = new();
    }

    // ── NOTICIAS ─────────────────────────────────────

    public class Noticia
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = "";
        public string Contenido { get; set; } = "";
        public string? ImagenUrl { get; set; }
        public string Tipo { get; set; } = "manual";
        public string Autor { get; set; } = "Admin";
        public bool Publicada { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    public class NoticiasViewModel
    {
        public List<Noticia> Noticias { get; set; } = new();
        public bool EsRedactor { get; set; }
    }

    public class NuevaNoticiaViewModel
    {
        public string Titulo { get; set; } = "";
        public string Contenido { get; set; } = "";
        public string? ImagenPrompt { get; set; }
        public bool GenerarImagen { get; set; } = true;
    }

    public class UsuarioRedactor
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string Rol { get; set; } = "redactor";
        public bool Activo { get; set; } = true;
    }

    // ── COMENTARIOS ──────────────────────────────────

    public class Comentario
    {
        public int Id { get; set; }
        public int NoticiaId { get; set; }
        public string Autor { get; set; } = "";
        public string? PaisFlag { get; set; }
        public string Contenido { get; set; } = "";
        public bool Aprobado { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    public class ComentarioViewModel
    {
        public string Autor { get; set; } = "";
        public string? PaisFlag { get; set; }
        public string Contenido { get; set; } = "";
    }

    // ── DETALLE DE EQUIPO ────────────────────────────
    public class EquipoDetalleViewModel
    {
        public Equipo Equipo { get; set; } = new();
        public PosicionViewModel? Posicion { get; set; }
        public List<Titulo> Titulos { get; set; } = new();
    }

    // ── TROFEOS (VIDRIERA) ───────────────────────────
    public class Trofeo
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string? ImagenUrl { get; set; }
        public string TipoTitulo { get; set; } = "";
        public int Orden { get; set; }
        public List<Titulo> Campeones { get; set; } = new();
    }

    // ── PREDICCIONES ─────────────────────────────────
    public class Prediccion
    {
        public int Id { get; set; }
        public int PartidoId { get; set; }
        public int DivisionId { get; set; }
        public string Autor { get; set; } = "";
        public string? PaisFlag { get; set; }
        public string Prediccion1X2 { get; set; } = ""; // L, E, V
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }
        public int? Puntos { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PartidoConPrediccionesViewModel
    {
        public Partido Partido { get; set; } = new();
        public List<Prediccion> Predicciones { get; set; } = new();
        public Prediccion? MiPrediccion { get; set; }
    }

    public class PrediccionesViewModel
    {
        public int DivisionId { get; set; }
        public List<PartidoConPrediccionesViewModel> ProximosPartidos { get; set; } = new();
        public List<RankingPronosticador> Ranking { get; set; } = new();
    }

    public class RankingPronosticador
    {
        public string Autor { get; set; } = "";
        public string? PaisFlag { get; set; }
        public int TotalPuntos { get; set; }
        public int Predicciones { get; set; }
        public int Aciertos1X2 { get; set; }
        public int AciertosExactos { get; set; }
    }

    // ── HEAD TO HEAD ─────────────────────────────────
    public class HeadToHeadViewModel
    {
        public Equipo EquipoA { get; set; } = new();
        public Equipo EquipoB { get; set; } = new();
        public List<EnfrentamientoDirecto> Enfrentamientos { get; set; } = new();
        public int VictoriasA { get; set; }
        public int VictoriasB { get; set; }
        public int Empates { get; set; }
        // Stats generales para comparar
        public PosicionViewModel? StatsA { get; set; }
        public PosicionViewModel? StatsB { get; set; }
        public List<Titulo> TitulosA { get; set; } = new();
        public List<Titulo> TitulosB { get; set; } = new();
    }

    public class EnfrentamientoDirecto
    {
        public DateTime? Fecha { get; set; }
        public int GolesA { get; set; }
        public int GolesB { get; set; }
        public string TemporadaNombre { get; set; } = "";
        public bool ALocal { get; set; } // true si EquipoA jugó de local
    }
}
