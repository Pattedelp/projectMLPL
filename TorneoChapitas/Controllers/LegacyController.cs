using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;

namespace TorneoAmigos.Controllers
{
    public class LegacyController : Controller
    {
        private readonly TemporadaRepository _repo;
        public LegacyController(TemporadaRepository repo) => _repo = repo;

        public IActionResult Index()
        {
            ViewBag.ActivePage = "legacy";
            var temporadas = _repo.GetLegacyTemporadas();
            return View(temporadas);
        }
    }
}
