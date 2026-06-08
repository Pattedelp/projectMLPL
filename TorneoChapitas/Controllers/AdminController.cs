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

        // ── PANEL PRINCIPAL ─────────────────────────────
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

        // ── HISTORIAL ───────────────────────────────────
        public IActionResult Historial()
        {
            ViewBag.ActivePage = "admin";
            var vm = new HistorialViewModel
            {
                Temporadas = _tempRepo.GetTodasLasTemporadas()
            };
            return View(vm);
        }

        public IActionResult TemporadaDetalle(int id)
        {
            ViewBag.ActivePage = "admin";
            var resultados = _tempRepo.GetResultadosTemporada(id);
            var temporadas = _tempRepo.GetTodasLasTemporadas();
            var temporada  = temporadas.FirstOrDefault(t => t.Id == id);
            if (temporada == null) return NotFound();

            var vm = new TemporadaDetalleViewModel
            {
                Temporada          = temporada,
                ResultadosPrimera  = resultados.Where(r => r.DivisionId == 1).ToList(),
                ResultadosNacionalB = resultados.Where(r => r.DivisionId == 2).ToList()
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

            // Obtener último número de temporada para sugerir el siguiente nombre
            var temporadas = _tempRepo.GetTodasLasTemporadas();
            int siguienteNum = (temporadas.FirstOrDefault()?.Numero ?? 0) + 1;

            var vm = new NuevaTemporadaViewModel
            {
                NumeroTemporada = siguienteNum,
                EquiposPrimera  = equiposPrimera.Select(e => new EquipoCheckbox
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
            {
                for (int i = 0; i < Math.Min(nuevosNombres.Count, nuevasDivisiones.Count); i++)
                {
                    if (!string.IsNullOrWhiteSpace(nuevosNombres[i]))
                        nuevos.Add((nuevosNombres[i].Trim(), nuevasDivisiones[i]));
                }
            }

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

        // ── COPAS ───────────────────────────────────────
        [HttpPost]
        public IActionResult SortearCopaArgentina()
        {
            var temporada = _tempRepo.GetTemporadaActiva();
            if (temporada == null) return Json(new { ok = false, msg = "No hay temporada activa" });

            var equiposPrimera = _repo.GetEquiposByDivision(1).Select(e => e.Id).ToList();
            var equiposB       = _repo.GetEquiposByDivision(2).Select(e => e.Id).ToList();
            var todos          = equiposPrimera.Concat(equiposB).ToList();

            try
            {
                _tempRepo.SortearCopaArgentina(temporada.Id, todos);
                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, msg = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult SortearSupercopa(int campeonId, int subcampeonId, int campeonCopaId)
        {
            var temporada = _tempRepo.GetTemporadaActiva();
            if (temporada == null) return Json(new { ok = false, msg = "No hay temporada activa" });

            try
            {
                _tempRepo.SortearSupercopa(temporada.Id, campeonId, subcampeonId, campeonCopaId);
                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, msg = ex.Message });
            }
        }
    }
}
