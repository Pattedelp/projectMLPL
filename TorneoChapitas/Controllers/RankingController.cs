using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;

namespace TorneoAmigos.Controllers
{
    public class RankingController : Controller
    {
        private readonly TemporadaRepository _repo;
        public RankingController(TemporadaRepository repo) => _repo = repo;

        public IActionResult Index(int temporadas = 5)
        {
            ViewBag.ActivePage = "ranking";
            ViewBag.Temporadas = temporadas;
            var ranking = _repo.GetRankingFifa(temporadas);
            return View(ranking);
        }
    }
}
