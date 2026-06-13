using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;

namespace TorneoAmigos.Controllers
{
    [Route("[controller]")]
    public class ResultadoController : Controller
    {
        private readonly TorneoRepository _repo;
        private readonly PrediccionesRepository _prediRepo;
        public ResultadoController(TorneoRepository repo, PrediccionesRepository prediRepo)
        {
            _repo = repo;
            _prediRepo = prediRepo;
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("Guardar")]
        public IActionResult Guardar([FromBody] GuardarResultadoDto dto)
        {
            if (dto == null || dto.GolesLocal < 0 || dto.GolesVisitante < 0)
                return Json(new { ok = false });
            var ok = _repo.CargarResultado(dto.PartidoId, dto.GolesLocal, dto.GolesVisitante);

            if (ok)
            {
                // Calcular puntos de predicciones para este partido
                var divisionId = _repo.GetDivisionIdDePartido(dto.PartidoId);
                if (divisionId > 0)
                    _prediRepo.CalcularPuntosPartido(dto.PartidoId, divisionId, dto.GolesLocal, dto.GolesVisitante);
            }

            return Json(new { ok });
        }

        [HttpGet("TablaViva")]
        public IActionResult TablaViva(int divisionId)
        {
            var tabla = _repo.GetTablaPosiciones(divisionId);
            return PartialView("~/Views/Shared/_TablaViva.cshtml", tabla);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("ToggleFecha")]
        public IActionResult ToggleFecha([FromBody] ToggleFechaDto dto)
        {
            if (dto == null) return Json(new { ok = false });
            var ok = _repo.ToggleFechaHabilitada(dto.FechaId, dto.Habilitada);
            return Json(new { ok });
        }
    }

    public class ToggleFechaDto
    {
        public int FechaId { get; set; }
        public bool Habilitada { get; set; }
    }

    public class GuardarResultadoDto
    {
        public int PartidoId { get; set; }
        public int GolesLocal { get; set; }
        public int GolesVisitante { get; set; }
    }
}
