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
            var palmares = _repo.GetPalmares();
            ViewBag.TodosLosTitulos = palmares.Equipos.SelectMany(e => e.Titulos).ToList();
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

            ViewBag.PartidosPrimera  = _repo.GetPartidosHistorico(temporada.Nombre, 1);
            ViewBag.PartidosNacional = _repo.GetPartidosHistorico(temporada.Nombre, 2);
            return View(vm);
        }
    }
}
