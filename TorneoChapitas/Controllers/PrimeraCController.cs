using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    public class PrimeraCController : Controller
    {
        private readonly TorneoRepository _repo;
        private readonly TemporadaRepository _tempRepo;

        public PrimeraCController(TorneoRepository repo, TemporadaRepository tempRepo)
        {
            _repo    = repo;
            _tempRepo = tempRepo;
        }

        private IActionResult CheckActiva()
        {
            if (!_tempRepo.PrimeraCActiva())
                return RedirectToAction("Index", "Home");
            return null!;
        }

        public IActionResult Posiciones()
        {
            var check = CheckActiva(); if (check != null) return check;
            ViewBag.ActivePage = "primera-c";
            var tabla = _repo.GetTablaPosiciones(3);
            return View(tabla);
        }

        public IActionResult Fixture()
        {
            var check = CheckActiva(); if (check != null) return check;
            ViewBag.ActivePage = "primera-c";
            ViewBag.EsAdmin = User.IsInRole("Admin");
            try
            {
                var vm = new DivisionViewModel
                {
                    Division        = _repo.GetDivisionById(3),
                    TablaPosiciones = _repo.GetTablaPosiciones(3),
                    Fixture         = _repo.GetFixture(3),
                    Equipos         = _repo.GetEquiposByDivision(3)
                };
                return View(vm);
            }
            catch
            {
                return View(new DivisionViewModel { Equipos = new(), Fixture = new(), TablaPosiciones = new() });
            }
        }

        public IActionResult Equipos()
        {
            var check = CheckActiva(); if (check != null) return check;
            ViewBag.ActivePage = "primera-c";
            var equipos = _repo.GetEquiposByDivision(3);
            return View(equipos);
        }
    }
}
