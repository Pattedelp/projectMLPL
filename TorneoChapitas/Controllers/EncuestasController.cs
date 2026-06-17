using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    public class EncuestasController : Controller
    {
        private readonly EncuestasRepository _repo;
        private readonly TorneoRepository    _torneoRepo;

        public EncuestasController(EncuestasRepository repo, TorneoRepository torneoRepo)
        {
            _repo       = repo;
            _torneoRepo = torneoRepo;
        }

        [HttpPost]
        public IActionResult Votar([FromBody] VotoDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Autor)) return Json(new { ok = false, msg = "Nombre requerido" });
            var ok = _repo.Votar(dto.EncuestaId, dto.OpcionId, dto.Autor);
            return Json(new { ok, msg = ok ? "Voto registrado" : "Ya votaste esta opción o alcanzaste el máximo" });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult CrearAutomatica(string tipo)
        {
            var equipos = tipo == "descenso_primera"
                ? _torneoRepo.GetEquiposByDivision(1)
                : _torneoRepo.GetEquiposByDivision(2);

            // Get active temporada id
            var temps = new TemporadaRepository(HttpContext.RequestServices.GetRequiredService<IConfiguration>());
            var temp = temps.GetTemporadaActiva();
            if (temp == null) return Json(new { ok = false, msg = "No hay temporada activa" });

            var id = _repo.CrearEncuestaAutomatica(temp.Id, tipo, equipos);
            return Json(new { ok = id > 0, msg = id > 0 ? "Encuesta creada" : "Ya existe esa encuesta para esta temporada" });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult Toggle(int id, bool activa)
        {
            _repo.ToggleEncuesta(id, activa);
            return Json(new { ok = true });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult Eliminar(int id)
        {
            _repo.EliminarEncuesta(id);
            return Json(new { ok = true });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public IActionResult CrearManual([FromBody] CrearEncuestaDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Pregunta) || dto.Opciones == null || dto.Opciones.Count < 2)
                return Json(new { ok = false, msg = "Pregunta y al menos 2 opciones requeridas" });
            var id = _repo.CrearEncuestaManual(dto.Pregunta, dto.MaxVotos, dto.TemporadaId, dto.Opciones);
            return Json(new { ok = id > 0 });
        }
    }

    public class VotoDto
    {
        public int EncuestaId { get; set; }
        public int OpcionId { get; set; }
        public string Autor { get; set; } = "";
    }

    public class CrearEncuestaDto
    {
        public string Pregunta { get; set; } = "";
        public int MaxVotos { get; set; } = 1;
        public int? TemporadaId { get; set; }
        public List<string> Opciones { get; set; } = new();
    }
}
