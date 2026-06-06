using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    public class NacionalBController : Controller
    {
        private readonly TorneoRepository _repo;
        private const int DIV = 2;
        public NacionalBController(TorneoRepository repo) => _repo = repo;

        public IActionResult Index() => RedirectToAction("Posiciones");

        public IActionResult Posiciones() => View("~/Views/PrimeraDivision/Posiciones.cshtml", new DivisionViewModel
        {
            Division = _repo.GetDivisionById(DIV),
            TablaPosiciones = _repo.GetTablaPosiciones(DIV)
        });

        public IActionResult Fixture() => View("~/Views/PrimeraDivision/Fixture.cshtml", new DivisionViewModel
        {
            Division = _repo.GetDivisionById(DIV),
            Fixture = _repo.GetFixture(DIV)
        });

        public IActionResult Equipos() => View("~/Views/PrimeraDivision/Equipos.cshtml", new DivisionViewModel
        {
            Division = _repo.GetDivisionById(DIV),
            Equipos = _repo.GetEquiposByDivision(DIV),
            TablaPosiciones = _repo.GetTablaPosiciones(DIV)
        });

        [HttpGet]
        public IActionResult CargarResultado(int id)
        {
            var p = _repo.GetPartidoById(id);
            if (p == null) return NotFound();
            return View("~/Views/PrimeraDivision/CargarResultado.cshtml", new CargarResultadoViewModel
            {
                PartidoId = p.Id,
                EquipoLocal = p.EquipoLocal?.Nombre ?? "",
                EquipoVisitante = p.EquipoVisitante?.Nombre ?? "",
                GolesLocal = p.GolesLocal ?? 0,
                GolesVisitante = p.GolesVisitante ?? 0
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CargarResultado(CargarResultadoViewModel vm)
        {
            if (!ModelState.IsValid) return View("~/Views/PrimeraDivision/CargarResultado.cshtml", vm);
            _repo.CargarResultado(vm.PartidoId, vm.GolesLocal, vm.GolesVisitante, vm.Observaciones);
            TempData["Mensaje"] = "¡Resultado cargado correctamente!";
            return RedirectToAction("Fixture");
        }
    }
}
