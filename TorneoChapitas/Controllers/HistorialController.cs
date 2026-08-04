using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    // Público — cualquiera puede ver el historial
    public class HistorialController : Controller
    {
        private readonly TemporadaRepository _repo;
        public HistorialController(TemporadaRepository repo) => _repo = repo;

        public IActionResult Index()
        {
            ViewBag.ActivePage = "historial";
            var vm = new HistorialViewModel
            {
                Temporadas = _repo.GetTodasLasTemporadas()
            };
            var palmares = _repo.GetPalmares();
            ViewBag.TodosLosTitulos = palmares.Equipos.SelectMany(e => e.Titulos).ToList();
            return View(vm);
        }

public IActionResult Detalle(int id)
{
    ViewBag.ActivePage = "historial";
    var resultados = _repo.GetResultadosTemporada(id);
    var temporadas = _repo.GetTodasLasTemporadas();
    var temporada  = temporadas.FirstOrDefault(t => t.Id == id);
    if (temporada == null) return NotFound();

    var vm = new TemporadaDetalleViewModel
    {
        Temporada           = temporada,
        ResultadosPrimera   = resultados.Where(r => r.DivisionId == 1).ToList(),
        ResultadosNacionalB = resultados.Where(r => r.DivisionId == 2).ToList(),
        ResultadosPrimeraC  = resultados.Where(r => r.DivisionId == 3).ToList()
    };

    ViewBag.PartidosPrimera  = _repo.GetPartidosHistorico(temporada.Nombre, 1);
    ViewBag.PartidosNacional = _repo.GetPartidosHistorico(temporada.Nombre, 2);
    ViewBag.PartidosPrimeraC = _repo.GetPartidosHistorico(temporada.Nombre, 3);

    // Si no hay resultados de C en temporada_resultados, calcularlos desde el histórico
    if (!vm.ResultadosPrimeraC.Any())
    {
        var partidosC = (ViewBag.PartidosPrimeraC as List<HistoricoPartido> ?? new())
            .Where(p => p.Torneo == "Liga").ToList();

        if (partidosC.Any())
        {
            var stats = new Dictionary<int, (string nombre, string flag, int pj, int v, int d, int gf, int gc)>();
            foreach (var p in partidosC)
            {
                if (!stats.ContainsKey(p.EquipoLocalId))
                    stats[p.EquipoLocalId] = (p.NombreLocal, p.FlagLocal, 0, 0, 0, 0, 0);
                if (!stats.ContainsKey(p.EquipoVisitanteId))
                    stats[p.EquipoVisitanteId] = (p.NombreVisitante, p.FlagVisitante, 0, 0, 0, 0, 0);

                var sl = stats[p.EquipoLocalId];
                var sv = stats[p.EquipoVisitanteId];
                bool lg = p.GolesLocal > p.GolesVisitante;

                stats[p.EquipoLocalId]     = (sl.nombre, sl.flag, sl.pj+1, lg?sl.v+1:sl.v, lg?sl.d:sl.d+1, sl.gf+p.GolesLocal,     sl.gc+p.GolesVisitante);
                stats[p.EquipoVisitanteId] = (sv.nombre, sv.flag, sv.pj+1, lg?sv.v:sv.v+1, lg?sv.d+1:sv.d, sv.gf+p.GolesVisitante, sv.gc+p.GolesLocal);
            }

            int posC = 1;
            vm.ResultadosPrimeraC = stats
                .OrderByDescending(x => x.Value.v)
                .ThenByDescending(x => x.Value.gf - x.Value.gc)
                .Select(x => new TemporadaResultado
                {
                    EquipoId        = x.Key,
                    NombreEquipo    = x.Value.nombre,
                    FlagCode        = x.Value.flag,
                    DivisionId      = 3,
                    NombreDivision  = "Primera C Nacional",
                    Posicion        = posC++,
                    Puntos          = x.Value.v * 3,
                    PartidosJugados = x.Value.pj,
                    Ganados         = x.Value.v,
                    Perdidos        = x.Value.d,
                    GolesFavor      = x.Value.gf,
                    GolesContra     = x.Value.gc,
                    Campeon         = false,
                    Ascendio        = false,
                    Descendio       = false
                })
                .ToList();

            if (vm.ResultadosPrimeraC.Any())
                vm.ResultadosPrimeraC[0].Campeon = true;
        }
    }

    var partidos0 = _repo.GetPartidosHistorico(temporada.Nombre, 0);
    ViewBag.PartidosCopa      = partidos0.Where(p => p.Torneo.StartsWith("Copa Argentina")).ToList();
    ViewBag.PartidosSupercopa = partidos0.Where(p => p.Torneo.StartsWith("Supercopa Argentina")).ToList();
    ViewBag.PartidosEspeciales = partidos0
        .Where(p => !p.Torneo.StartsWith("Copa Argentina") && !p.Torneo.StartsWith("Supercopa Argentina"))
        .ToList();

    var palmares = _repo.GetPalmares();
    ViewBag.TitulosTemporada = palmares.Equipos.SelectMany(e => e.Titulos)
        .Where(t => t.TemporadaNombre == temporada.Nombre).ToList();

    return View(vm);
}
    }
}
