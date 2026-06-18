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
                    .Where(g => g.Fecha.Habilitada)
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
            ViewBag.RankingPrimera  = _prediRepo.GetRankingPorDivision(1);
            ViewBag.RankingNacional = _prediRepo.GetRankingPorDivision(2);
            ViewBag.RankingCopa     = _prediRepo.GetRankingPorDivision(100);

            // Ranking por temporada activa
            var tempActiva = _tempRepo.GetTemporadaActiva();
            if (tempActiva != null)
                ViewBag.RankingTemporada = _prediRepo.GetRankingPorTemporada(tempActiva.Id);
            ViewBag.TemporadaNombre = tempActiva?.Nombre ?? "";

            return View(vm);
        }

        [HttpGet]
        public IActionResult MisPredicciones(string autor)
        {
            if (string.IsNullOrWhiteSpace(autor))
                return Json(new { ok = false, msg = "Ingresá tu nombre" });
            var lista = _prediRepo.GetMisPredicciones(autor);
            return Json(new { ok = true, data = lista });
        }

        [HttpPost]
        public IActionResult Predecir([FromBody] PredicirDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Autor))
                return Json(new { ok = false, msg = "Nombre requerido" });

            var ok = _prediRepo.GuardarPrediccion(dto.PartidoId, dto.DivisionId, dto.Autor,
                dto.PaisFlag, dto.Prediccion1x2, dto.GolesLocal, dto.GolesVisitante);
            return Json(new { ok, msg = ok ? "✅ Predicción guardada" : "Ya predijiste este partido" });
        }
    }

    public class PredicirDto
    {
        public int PartidoId { get; set; }
        public int DivisionId { get; set; }
        public string Autor { get; set; } = "";
        public string? PaisFlag { get; set; }
        public string Prediccion1x2 { get; set; } = "";
        public int? GolesLocal { get; set; }
        public int? GolesVisitante { get; set; }
    }
}
