using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;

namespace TorneoAmigos.Controllers
{
    [Route("[controller]")]
    public class ResultadoController : Controller
    {
        private readonly TorneoRepository _repo;
        public ResultadoController(TorneoRepository repo) => _repo = repo;

        // Solo admins pueden guardar resultados
        [Authorize(Roles = "Admin")]
        [HttpPost("Guardar")]
        public IActionResult Guardar([FromBody] GuardarResultadoDto dto)
        {
            if (dto == null || dto.GolesLocal < 0 || dto.GolesVisitante < 0)
                return Json(new { ok = false, msg = "Datos inválidos" });

            var ok = _repo.CargarResultado(dto.PartidoId, dto.GolesLocal, dto.GolesVisitante);
            return Json(new { ok });
        }

        // Devuelve HTML de la tabla actualizada (público)
        [HttpGet("TablaViva")]
        public IActionResult TablaViva(int divisionId)
        {
            var tabla = _repo.GetTablaPosiciones(divisionId);
            return PartialView("~/Views/Shared/_TablaViva.cshtml", tabla);
        }
    }

    public class GuardarResultadoDto
    {
        public int PartidoId { get; set; }
        public int GolesLocal { get; set; }
        public int GolesVisitante { get; set; }
    }
}
