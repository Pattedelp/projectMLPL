using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    public class PrediccionesController : Controller
    {
        private readonly TorneoRepository _repo;
        private readonly TemporadaRepository _tempRepo;
        private readonly PrediccionesRepository _prediRepo;

        public PrediccionesController(TorneoRepository repo, TemporadaRepository tempRepo, PrediccionesRepository prediRepo)
        {
            _repo      = repo;
            _tempRepo  = tempRepo;
            _prediRepo = prediRepo;
        }

        public IActionResult Index(int division = 1)
        {
            ViewBag.ActivePage = "predicciones";
            var vm = new PrediccionesViewModel { DivisionId = division };

            if (division == 100 || division == 101)
            {
                // Copa Argentina (100) o Supercopa (101)
                var tipoCopa = division == 100 ? "copa_argentina" : "supercopa";
                var partidosCopa = _tempRepo.GetPartidosCopaPendientes(tipoCopa).Take(10).ToList();

                foreach (var cp in partidosCopa)
                {
                    var partido = new Partido
                    {
                        Id = cp.Id,
                        EquipoLocalId = cp.EquipoLocalId ?? 0,
                        EquipoVisitanteId = cp.EquipoVisitanteId ?? 0,
                        EquipoLocal = new Equipo { Id = cp.EquipoLocalId ?? 0, Nombre = cp.NombreLocal, FlagCode = cp.FlagLocal },
                        EquipoVisitante = new Equipo { Id = cp.EquipoVisitanteId ?? 0, Nombre = cp.NombreVisitante, FlagCode = cp.FlagVisitante },
                        Jugado = cp.Jugado
                    };

                    vm.ProximosPartidos.Add(new PartidoConPrediccionesViewModel
                    {
                        Partido      = partido,
                        Predicciones = _prediRepo.GetPrediccionesPorPartido(cp.Id, division)
                    });
                }
            }
            else
            {
                // Próximos partidos de liga sin jugar — solo de fechas VISIBLES
                var fixture = _repo.GetFixture(division);
                var proximos = fixture
                    .Where(g => g.Fecha.Visible)
                    .SelectMany(g => g.Partidos)
                    .Where(p => !p.Jugado && p.TipoPartido == "regular")
                    .Take(10)
                    .ToList();

                foreach (var p in proximos)
                {
                    vm.ProximosPartidos.Add(new PartidoConPrediccionesViewModel
                    {
                        Partido       = p,
                        Predicciones  = _prediRepo.GetPrediccionesPorPartido(p.Id, division)
                    });
                }
            }

            vm.Ranking = _prediRepo.GetRanking();

            return View(vm);
        }

        [HttpPost]
        public IActionResult Predecir(int partidoId, int divisionId, string autor, string? paisFlag,
            string prediccion1x2, int? golesLocal, int? golesVisitante)
        {
            if (string.IsNullOrWhiteSpace(autor))
                return RedirectToAction("Index", new { division = divisionId });

            _prediRepo.GuardarPrediccion(partidoId, divisionId, autor, paisFlag, prediccion1x2, golesLocal, golesVisitante);
            return RedirectToAction("Index", new { division = divisionId });
        }
    }
}
