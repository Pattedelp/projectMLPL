using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    // Público — cualquiera puede ver el historial
    public class HistorialController : Controller
    {
        private readonly TemporadaRepository _repo;
        public HistorialController(TemporadaRepository repo) => _repo = repo;

        public IActionResult Index()
        {
            ViewBag.ActivePage = "historial";
            var vm = new HistorialViewModel
            {
                Temporadas = _repo.GetTodasLasTemporadas()
            };
            return View(vm);
        }

        public IActionResult Detalle(int id)
        {
            ViewBag.ActivePage = "historial";
            var resultados = _repo.GetResultadosTemporada(id);
            var temporadas = _repo.GetTodasLasTemporadas();
            var temporada  = temporadas.FirstOrDefault(t => t.Id == id);
            if (temporada == null) return NotFound();

            var vm = new TemporadaDetalleViewModel
            {
                Temporada           = temporada,
                ResultadosPrimera   = resultados.Where(r => r.DivisionId == 1).ToList(),
                ResultadosNacionalB = resultados.Where(r => r.DivisionId == 2).ToList()
            };
            return View(vm);
        }
    }
}
