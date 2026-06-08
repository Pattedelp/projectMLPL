using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly TorneoRepository _repo;
        private readonly TemporadaRepository _tempRepo;

        public AdminController(TorneoRepository repo, TemporadaRepository tempRepo)
        {
            _repo     = repo;
            _tempRepo = tempRepo;
        }

        public IActionResult Index()
        {
            ViewBag.ActivePage = "admin";
            var vm = new HistorialViewModel
            {
                Temporadas      = _tempRepo.GetTodasLasTemporadas(),
                TemporadaActiva = _tempRepo.GetTemporadaActiva()
            };
            return View(vm);
        }

        // ── FINALIZAR TEMPORADA ─────────────────────────
        [HttpPost]
        public IActionResult FinalizarTemporada()
        {
            var temporada = _tempRepo.GetTemporadaActiva();
            if (temporada == null) return Json(new { ok = false, msg = "No hay temporada activa" });

            var tablaPrimera = _repo.GetTablaPosiciones(1);
            var tablaB       = _repo.GetTablaPosiciones(2);
            var ok = _tempRepo.FinalizarTemporada(temporada.Id, tablaPrimera, tablaB);
            return Json(new { ok, msg = ok ? "Temporada finalizada" : "Error al finalizar" });
        }

        // ── NUEVA TEMPORADA ─────────────────────────────
        [HttpGet]
        public IActionResult NuevaTemporada()
        {
            ViewBag.ActivePage = "admin";
            var equiposPrimera = _repo.GetEquiposByDivision(1);
            var equiposB       = _repo.GetEquiposByDivision(2);
            var temporadas     = _tempRepo.GetTodasLasTemporadas();
            int siguienteNum   = (temporadas.FirstOrDefault()?.Numero ?? 0) + 1;

            var vm = new NuevaTemporadaViewModel
            {
                NumeroTemporada  = siguienteNum,
                EquiposPrimera   = equiposPrimera.Select(e => new EquipoCheckbox
                {
                    Id = e.Id, Nombre = e.Nombre, FlagCode = e.FlagCode,
                    DivisionId = e.DivisionId, Seleccionado = true
                }).ToList(),
                EquiposNacionalB = equiposB.Select(e => new EquipoCheckbox
                {
                    Id = e.Id, Nombre = e.Nombre, FlagCode = e.FlagCode,
                    DivisionId = e.DivisionId, Seleccionado = true
                }).ToList()
            };
            return View(vm);
        }

        [HttpPost]
        public IActionResult NuevaTemporada(
            string nombreTemporada,
            List<int>? equiposPrimera,
            List<int>? equiposB,
            List<string>? nuevosNombres,
            List<int>? nuevasDivisiones)
        {
            equiposPrimera ??= new();
            equiposB       ??= new();

            var nuevos = new List<(string nombre, int divisionId)>();
            if (nuevosNombres != null && nuevasDivisiones != null)
                for (int i = 0; i < Math.Min(nuevosNombres.Count, nuevasDivisiones.Count); i++)
                    if (!string.IsNullOrWhiteSpace(nuevosNombres[i]))
                        nuevos.Add((nuevosNombres[i].Trim(), nuevasDivisiones[i]));

            try
            {
                _tempRepo.CrearNuevaTemporada(nombreTemporada, equiposPrimera, equiposB, nuevos);
                TempData["Mensaje"] = "¡Nueva temporada creada con fixture generado!";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
                return RedirectToAction("NuevaTemporada");
            }
        }

        // ── COPA ARGENTINA ──────────────────────────────
        [HttpGet]
        public IActionResult SortearCopa()
        {
            ViewBag.ActivePage = "admin";
            var todos = _repo.GetEquiposByDivision(1)
                .Concat(_repo.GetEquiposByDivision(2))
                .Select(e => new EquipoCheckbox
                {
                    Id = e.Id, Nombre = e.Nombre, FlagCode = e.FlagCode,
                    DivisionId = e.DivisionId, Seleccionado = true
                }).ToList();
            return View(todos);
        }

        [HttpPost]
        public IActionResult SortearCopaArgentina([FromBody] SortearCopaDto? dto)
        {
            var temporada = _tempRepo.GetTemporadaActiva();
            if (temporada == null) return Json(new { ok = false, msg = "No hay temporada activa" });

            List<int> equipos;
            if (dto?.Equipos != null && dto.Equipos.Any())
                equipos = dto.Equipos;
            else
                equipos = _repo.GetEquiposByDivision(1).Select(e => e.Id)
                    .Concat(_repo.GetEquiposByDivision(2).Select(e => e.Id)).ToList();

            try
            {
                _tempRepo.SortearCopaArgentina(temporada.Id, equipos);
                return Json(new { ok = true });
            }
            catch (Exception ex) { return Json(new { ok = false, msg = ex.Message }); }
        }

        // ── SUPERCOPA ───────────────────────────────────
        [HttpGet]
        public IActionResult ConfigurarSupercopa()
        {
            ViewBag.ActivePage = "admin";
            return View();
        }

        [HttpPost]
        public IActionResult SortearSupercopa([FromBody] SortearSupercopaDto dto)
        {
            var temporada = _tempRepo.GetTemporadaActiva();
            if (temporada == null) return Json(new { ok = false, msg = "No hay temporada activa" });

            try
            {
                // Lógica: si campeón == campeón copa, agregar semifinal
                bool mismoChampion = dto.CampeonId == dto.CampeonCopaId;
                _tempRepo.SortearSupercopa(temporada.Id, dto.CampeonId, dto.SubcampeonId, dto.CampeonCopaId, mismoChampion);
                return Json(new { ok = true });
            }
            catch (Exception ex) { return Json(new { ok = false, msg = ex.Message }); }
        }

        // ── API helpers para ConfigurarSupercopa ────────
        [HttpGet]
        public IActionResult GetEquiposPrimera()
        {
            var equipos = _repo.GetEquiposByDivision(1)
                .Select(e => new { id = e.Id, nombre = e.Nombre, flagCode = e.FlagCode });
            return Json(equipos);
        }

        [HttpGet]
        public IActionResult GetEquiposTodos()
        {
            var todos = _repo.GetEquiposByDivision(1)
                .Concat(_repo.GetEquiposByDivision(2))
                .Select(e => new { id = e.Id, nombre = e.Nombre, flagCode = e.FlagCode });
            return Json(todos);
        }
    }

    public class SortearCopaDto
    {
        public List<int> Equipos { get; set; } = new();
    }

    public class SortearSupercopaDto
    {
        public int CampeonId { get; set; }
        public int SubcampeonId { get; set; }
        public int CampeonCopaId { get; set; }
    }
}
