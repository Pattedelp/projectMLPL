using Microsoft.AspNetCore.Mvc;

namespace TorneoAmigos.Controllers
{
    public class PracticaController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.ActivePage = "practica";
            return View();
        }
    }
}
