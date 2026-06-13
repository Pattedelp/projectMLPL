using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    public class EquiposController : Controller
    {
        private readonly TorneoRepository _repo;
        private readonly TemporadaRepository _tempRepo;

        public EquiposController(TorneoRepository repo, TemporadaRepository tempRepo)
        {
            _repo     = repo;
            _tempRepo = tempRepo;
        }

        public IActionResult Detalle(int id)
        {
            var equipo = _repo.GetEquipoById(id);
            if (equipo == null) return NotFound();

            ViewBag.ActivePage = equipo.DivisionId == 1 ? "primera" : "nacb";

            // Posición actual en su división (si tiene partidos)
            var tabla = _repo.GetTablaPosiciones(equipo.DivisionId);
            var posicion = tabla.FirstOrDefault(t => t.EquipoId == id);

            // Títulos históricos
            var titulos = _tempRepo.GetTitulosPorEquipo(equipo.Nombre);

            // Lista de todos los equipos activos para el selector de Head to Head
            ViewBag.TodosLosEquipos = _repo.GetEquiposByDivision(1)
                .Concat(_repo.GetEquiposByDivision(2))
                .Where(e => e.Id != id)
                .OrderBy(e => e.Nombre)
                .ToList();

            var vm = new EquipoDetalleViewModel
            {
                Equipo    = equipo,
                Posicion  = posicion,
                Titulos   = titulos
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult HeadToHead(int equipoAId, int equipoBId)
        {
            var vm = _repo.GetHeadToHead(equipoAId, equipoBId);
            if (vm == null) return Json(new { ok = false });
            return PartialView("_HeadToHead", vm);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult ActualizarDescripcion([FromBody] ActualizarDescripcionDto dto)
        {
            var ok = _repo.ActualizarDescripcionEquipo(dto.EquipoId, dto.Descripcion ?? "");
            return Json(new { ok });
        }
    }

    public class ActualizarDescripcionDto
    {
        public int EquipoId { get; set; }
        public string? Descripcion { get; set; }
    }
}
