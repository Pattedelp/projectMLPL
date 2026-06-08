using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    public class CopasController : Controller
    {
        private readonly TemporadaRepository _repo;
        public CopasController(TemporadaRepository repo) => _repo = repo;

        public IActionResult Index() => RedirectToAction("CopaArgentina");

        public IActionResult CopaArgentina()
        {
            ViewBag.ActivePage = "copas";
            var vm = _repo.GetCopaFull("copa_argentina");
            if (vm == null) return View("SinCopa", "Copa Argentina");
            vm.EsAdmin = User.IsInRole("Admin");
            return View(vm);
        }

        public IActionResult SupercopaArgentina()
        {
            ViewBag.ActivePage = "copas";
            var vm = _repo.GetCopaFull("supercopa");
            if (vm == null) return View("SinCopa", "Supercopa Argentina");
            vm.EsAdmin = User.IsInRole("Admin");
            return View(vm);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult GuardarResultadoCopa([FromBody] ResultadoCopaDto dto)
        {
            if (dto == null) return Json(new { ok = false });
            var ok = _repo.GuardarResultadoCopa(dto.PartidoId, dto.GolesLocal, dto.GolesVisitante);
            return Json(new { ok });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult ToggleRonda([FromBody] ToggleRondaDto dto)
        {
            if (dto == null) return Json(new { ok = false });
            var ok = _repo.ToggleRondaHabilitada(dto.RondaId, dto.Habilitada);
            return Json(new { ok });
        }
    }

    public class ResultadoCopaDto
    {
        public int PartidoId { get; set; }
        public int GolesLocal { get; set; }
        public int GolesVisitante { get; set; }
    }

    public class ToggleRondaDto
    {
        public int RondaId { get; set; }
        public bool Habilitada { get; set; }
    }
}
