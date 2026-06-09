using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    public class NoticiasController : Controller
    {
        private readonly NoticiasRepository _repo;
        private readonly TorneoRepository   _torneo;

        public NoticiasController(NoticiasRepository repo, TorneoRepository torneo)
        {
            _repo   = repo;
            _torneo = torneo;
        }

        // ── PÚBLICO ─────────────────────────────────────

        public IActionResult Index()
        {
            ViewBag.ActivePage = "noticias";
            var vm = new NoticiasViewModel
            {
                Noticias    = _repo.GetNoticias(soloPublicadas: true),
                EsRedactor  = User.IsInRole("Redactor") || User.IsInRole("Admin")
            };
            return View(vm);
        }

        public IActionResult Detalle(int id)
        {
            ViewBag.ActivePage = "noticias";
            var noticia = _repo.GetNoticiaById(id);
            if (noticia == null) return NotFound();
            return View(noticia);
        }

        // ── LOGIN REDACTOR ───────────────────────────────

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.IsInRole("Redactor") || User.IsInRole("Admin"))
                return RedirectToAction("Redaccion");
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            var usuario = _repo.ValidarLogin(username, password);
            if (usuario != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, usuario.Username),
                    new Claim(ClaimTypes.Role, "Redactor")
                };
                var identity  = new ClaimsIdentity(claims, "RedactorCookie");
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync("RedactorCookie", principal,
                    new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) });

                return Redirect(returnUrl ?? "/Noticias/Redaccion");
            }

            ViewBag.Error     = "Usuario o contraseña incorrectos.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("RedactorCookie");
            return RedirectToAction("Index");
        }

        // ── REDACCIÓN (requiere login redactor o admin) ──

        [Authorize(AuthenticationSchemes = "RedactorCookie,Cookies", Roles = "Redactor,Admin")]
        public IActionResult Redaccion()
        {
            ViewBag.ActivePage = "noticias";
            var vm = new NoticiasViewModel
            {
                Noticias   = _repo.GetNoticias(soloPublicadas: false),
                EsRedactor = true
            };
            return View(vm);
        }

        [Authorize(AuthenticationSchemes = "RedactorCookie,Cookies", Roles = "Redactor,Admin")]
        [HttpGet]
        public IActionResult Nueva()
        {
            ViewBag.ActivePage = "noticias";
            return View(new NuevaNoticiaViewModel());
        }

        [Authorize(AuthenticationSchemes = "RedactorCookie,Cookies", Roles = "Redactor,Admin")]
        [HttpPost]
        public IActionResult Nueva(NuevaNoticiaViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Titulo) || string.IsNullOrWhiteSpace(vm.Contenido))
            {
                ViewBag.Error = "El título y el contenido son obligatorios.";
                return View(vm);
            }

            string? imagenUrl = null;
            if (vm.GenerarImagen)
            {
                var contexto = string.IsNullOrEmpty(vm.ImagenPrompt) ? vm.Titulo : vm.ImagenPrompt;
                imagenUrl = _repo.GenerarImagenUrl(contexto);
            }

            var autor = User.Identity?.Name ?? "Redactor";
            _repo.CrearNoticia(vm.Titulo, vm.Contenido, imagenUrl, "manual", autor);
            TempData["Mensaje"] = "¡Noticia publicada!";
            return RedirectToAction("Redaccion");
        }

        // ── GENERAR AUTOMÁTICA ───────────────────────────

        [Authorize(AuthenticationSchemes = "RedactorCookie,Cookies", Roles = "Redactor,Admin")]
        [HttpPost]
        public IActionResult GenerarAutomatica([FromBody] GenerarNoticiaDto dto)
        {
            var tablaPrimera = _torneo.GetTablaPosiciones(1);
            var tablaB       = _torneo.GetTablaPosiciones(2);
            var tipo         = dto?.Tipo ?? "tabla";

            var titulo    = _repo.GenerarTituloNoticia(tablaPrimera, tipo);
            var contenido = _repo.GenerarTextoNoticia(tablaPrimera, tablaB, tipo);
            var contexto  = tipo == "lider" && tablaPrimera.Any()
                ? $"champion leader {tablaPrimera[0].NombreEquipo}"
                : tipo == "descenso" ? "relegation battle tension" : "standings table update";
            var imagenUrl = _repo.GenerarImagenUrl(contexto);

            var autor = User.Identity?.Name ?? "Redactor";
            var id    = _repo.CrearNoticia(titulo, contenido, imagenUrl, "automatica", autor);

            return Json(new { ok = true, id, titulo, contenido, imagenUrl });
        }

        [Authorize(AuthenticationSchemes = "RedactorCookie,Cookies", Roles = "Redactor,Admin")]
        [HttpPost]
        public IActionResult Eliminar(int id)
        {
            _repo.EliminarNoticia(id);
            return RedirectToAction("Redaccion");
        }

        [Authorize(AuthenticationSchemes = "RedactorCookie,Cookies", Roles = "Redactor,Admin")]
        [HttpPost]
        public IActionResult TogglePublicada(int id)
        {
            _repo.TogglePublicada(id);
            return RedirectToAction("Redaccion");
        }
    }

    // ── ADMIN: gestión de redactores ─────────────────────
    [Authorize(Roles = "Admin")]
    public class RedactoresController : Controller
    {
        private readonly NoticiasRepository _repo;
        public RedactoresController(NoticiasRepository repo) => _repo = repo;

        public IActionResult Index()
        {
            ViewBag.ActivePage = "admin";
            return View(_repo.GetUsuarios());
        }

        [HttpPost]
        public IActionResult Crear(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                TempData["Error"] = "Usuario y contraseña son obligatorios.";
                return RedirectToAction("Index");
            }
            var ok = _repo.CrearUsuario(username, password);
            TempData["Mensaje"] = ok ? $"Redactor '{username}' creado." : "Error: el usuario ya existe.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult CambiarPassword(string username, string password)
        {
            _repo.CambiarPassword(username, password);
            TempData["Mensaje"] = "Contraseña actualizada.";
            return RedirectToAction("Index");
        }
    }

    public class GenerarNoticiaDto
    {
        public string Tipo { get; set; } = "tabla";
    }
}
