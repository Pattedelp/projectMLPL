using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    public class EstadisticasController : Controller
    {
        private readonly TemporadaRepository _repo;
        public EstadisticasController(TemporadaRepository repo) => _repo = repo;

        public IActionResult Index()
        {
            ViewBag.ActivePage = "estadisticas";
            var vm = new EstadisticasViewModel
            {
                Palmares       = _repo.GetPalmares(),
                RankingAllTime = _repo.GetRankingAllTime(),
                Historiales    = _repo.GetHistorialesCompletos()
            };
            return View(vm);
        }
    }
}
