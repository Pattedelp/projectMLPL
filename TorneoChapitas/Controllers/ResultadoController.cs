using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    [Route("[controller]")]
    public class ResultadoController : Controller
    {
        private readonly TorneoRepository _repo;
        private readonly PrediccionesRepository _prediRepo;
        private readonly TemporadaRepository _tempRepo;
        public ResultadoController(TorneoRepository repo, PrediccionesRepository prediRepo, TemporadaRepository tempRepo)
        {
            _repo      = repo;
            _prediRepo = prediRepo;
            _tempRepo  = tempRepo;
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
                var divisionId = _repo.GetDivisionIdDePartido(dto.PartidoId);
                if (divisionId > 0)
                    _prediRepo.CalcularPuntosPartido(dto.PartidoId, divisionId, dto.GolesLocal, dto.GolesVisitante);

                // Si es partido de promoción, actualizar automáticamente el borrador de cierre
                var tipoPartido = _repo.GetTipoPartido(dto.PartidoId);
                if (tipoPartido == "promocion")
                {
                    var temporada = _tempRepo.GetTemporadaActiva();
                    if (temporada != null)
                    {
                        var partido = _repo.GetPartidoById(dto.PartidoId);
                        if (partido != null)
                        {
                            // Ganador de la B asciende, perdedor de Primera se queda (no hay cambio automático)
                            // pero actualizamos el cierre con los valores correctos
                            var cierre = _tempRepo.GetCierre(temporada.Id) ?? new TemporadaCierre { TemporadaId = temporada.Id };
                            bool localGana = dto.GolesLocal > dto.GolesVisitante;

                            // Determinar quién es de Primera y quién de la B
                            // El local generalmente es de la B (ganador del reducido)
                            var tablaB = _repo.GetTablaPosiciones(2);
                            bool localEsB = tablaB.Any(t => t.EquipoId == partido.EquipoLocalId);

                            int? ascensoId  = localEsB && localGana  ? partido.EquipoLocalId   :
                                             !localEsB && !localGana ? partido.EquipoVisitanteId : null;
                            int? descensoId = localEsB && localGana  ? partido.EquipoVisitanteId :
                                             !localEsB && !localGana ? partido.EquipoLocalId    : null;

                            if (ascensoId.HasValue)  cierre.Ascenso1Id  = ascensoId;
                            if (descensoId.HasValue) cierre.Descenso1Id = descensoId;
                            _tempRepo.GuardarCierre(cierre);
                        }
                    }
                }
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
