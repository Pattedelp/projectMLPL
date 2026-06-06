using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    public class CopasController : Controller
    {
        private readonly TorneoRepository _repo;
        public CopasController(TorneoRepository repo) => _repo = repo;

        public IActionResult Index() => RedirectToAction("CopaArgentina");

        public IActionResult CopaArgentina()
        {
            ViewBag.ActivePage = "copas";
            var vm = BuildBracketVacio("Copa Argentina", "🏆", 10);
            return View(vm);
        }

        public IActionResult SupercopaArgentina()
        {
            ViewBag.ActivePage = "copas";
            var vm = BuildBracketVacio("Supercopa Argentina", "⭐", 9);
            return View("CopaArgentina", vm);
        }

        // Arma un bracket vacío con los equipos actuales
        private CopaViewModel BuildBracketVacio(string nombre, string icono, int cantEquipos)
        {
            // Por ahora bracket con estructura fija - se llenará cuando arranque la copa
            var tbd = new BracketTeam { Nombre = "Por definir", TBD = true };

            CopaViewModel vm = new()
            {
                NombreCopa = nombre,
                Icono = icono,
                Rondas = new()
                {
                    new BracketRound
                    {
                        Nombre = "Fase Previa",
                        Partidos = Enumerable.Range(1, 4).Select(i => new BracketMatch
                        {
                            Id = i,
                            Local     = new BracketTeam { Nombre = "Por definir", TBD = true },
                            Visitante = new BracketTeam { Nombre = "Por definir", TBD = true },
                            Jugado = false
                        }).ToList()
                    },
                    new BracketRound
                    {
                        Nombre = "Cuartos de Final",
                        Partidos = Enumerable.Range(1, 4).Select(i => new BracketMatch
                        {
                            Id = i + 10,
                            Local     = new BracketTeam { Nombre = "Por definir", TBD = true },
                            Visitante = new BracketTeam { Nombre = "Por definir", TBD = true },
                            Jugado = false
                        }).ToList()
                    },
                    new BracketRound
                    {
                        Nombre = "Semifinales",
                        Partidos = Enumerable.Range(1, 2).Select(i => new BracketMatch
                        {
                            Id = i + 20,
                            Local     = new BracketTeam { Nombre = "Por definir", TBD = true },
                            Visitante = new BracketTeam { Nombre = "Por definir", TBD = true },
                            Jugado = false
                        }).ToList()
                    },
                    new BracketRound
                    {
                        Nombre = "Final",
                        Partidos = new List<BracketMatch>
                        {
                            new BracketMatch
                            {
                                Id = 99,
                                Local     = new BracketTeam { Nombre = "Por definir", TBD = true },
                                Visitante = new BracketTeam { Nombre = "Por definir", TBD = true },
                                Jugado = false
                            }
                        }
                    }
                }
            };
            return vm;
        }
    }
}
