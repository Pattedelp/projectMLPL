using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    public class HomeController : Controller
    {
        private readonly TorneoRepository    _repo;
        private readonly NoticiasRepository  _noticias;
        private readonly EncuestasRepository _encuestas;
        private readonly TemporadaRepository _tempRepo;

        public HomeController(TorneoRepository repo, NoticiasRepository noticias,
            EncuestasRepository encuestas, TemporadaRepository tempRepo)
        {
            _repo      = repo;
            _noticias  = noticias;
            _encuestas = encuestas;
            _tempRepo  = tempRepo;
        }

        public IActionResult Index()
        {
            var vm = new HomeViewModel
            {
                PrimeraDivision      = BuildVM(1),
                NacionalB            = BuildVM(2),
                TotalPartidosJugados = _repo.GetTotalPartidosJugados(),
                TotalGoles           = _repo.GetTotalGoles(),
                UltimasNoticias      = _noticias.GetNoticias(soloPublicadas: true).Take(6).ToList()
            };
            ViewBag.Encuestas   = _encuestas.GetEncuestasActivas();
            ViewBag.RankingFifa = null; // Moved to /Ranking page
            return View(vm);
        }

        private DivisionViewModel BuildVM(int id) => new()
        {
            Division         = _repo.GetDivisionById(id),
            TablaPosiciones  = _repo.GetTablaPosiciones(id),
            Fixture          = _repo.GetFixture(id),
            Equipos          = _repo.GetEquiposByDivision(id)
        };
    }
}
