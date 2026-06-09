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
        private readonly IConfiguration     _config;

        public NoticiasController(NoticiasRepository repo, TorneoRepository torneo, IConfiguration config)
        {
            _repo   = repo;
            _torneo = torneo;
            _config = config;
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
        public async Task<IActionResult> Nueva(NuevaNoticiaViewModel vm, IFormFile? imagenArchivo)
        {
            if (string.IsNullOrWhiteSpace(vm.Titulo) || string.IsNullOrWhiteSpace(vm.Contenido))
            {
                ViewBag.Error = "El título y el contenido son obligatorios.";
                return View(vm);
            }

            string? imagenUrl = null;

            if (imagenArchivo != null && imagenArchivo.Length > 0)
            {
                var ext     = Path.GetExtension(imagenArchivo.FileName).ToLower();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                if (!allowed.Contains(ext)) { ViewBag.Error = "Formato no permitido. Usá JPG, PNG o WebP."; return View(vm); }
                var fileName = $"noticia_{DateTime.Now.Ticks}{ext}";
                var folder   = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "noticias");
                Directory.CreateDirectory(folder);
                using var stream = new FileStream(Path.Combine(folder, fileName), FileMode.Create);
                await imagenArchivo.CopyToAsync(stream);
                imagenUrl = $"/img/noticias/{fileName}";
            }
            else if (vm.GenerarImagen)
            {
                var contexto     = string.IsNullOrEmpty(vm.ImagenPrompt) ? vm.Titulo : vm.ImagenPrompt;
                var stabilityKey = _config["STABILITY_API_KEY"]
                    ?? Environment.GetEnvironmentVariable("STABILITY_API_KEY");
                imagenUrl = await _repo.GenerarImagenUrlIA(contexto, stabilityKey);
            }

            var autor = User.Identity?.Name ?? "Redactor";
            _repo.CrearNoticia(vm.Titulo, vm.Contenido, imagenUrl, "manual", autor);
            TempData["Mensaje"] = "¡Noticia publicada!";
            return RedirectToAction("Redaccion");
        }

        // ── GENERAR AUTOMÁTICA ───────────────────────────

        [Authorize(AuthenticationSchemes = "RedactorCookie,Cookies", Roles = "Redactor,Admin")]
        [HttpPost]
        public async Task<IActionResult> GenerarAutomatica([FromBody] GenerarNoticiaDto dto)
        {
            var tablaPrimera = _torneo.GetTablaPosiciones(1);
            var tablaB       = _torneo.GetTablaPosiciones(2);
            var tipo         = dto?.Tipo ?? "tabla";

            var titulo    = _repo.GenerarTituloNoticia(tablaPrimera, tipo);
            var contenido = _repo.GenerarTextoNoticia(tablaPrimera, tablaB, tipo);
            var contexto  = tipo == "lider" && tablaPrimera.Any()
                ? $"champion leader {tablaPrimera[0].NombreEquipo}"
                : tipo == "descenso" ? "relegation battle tension" : "standings table update";

            // Intentar con Stability AI, fallback a Unsplash
            var stabilityKey = _config["STABILITY_API_KEY"]
                ?? Environment.GetEnvironmentVariable("STABILITY_API_KEY");
            var imagenUrl = await _repo.GenerarImagenUrlIA(contexto, stabilityKey);

            var autor = User.Identity?.Name ?? "Redactor";
            var id    = _repo.CrearNoticia(titulo, contenido, imagenUrl, "automatica", autor);

            return Json(new { ok = true, id, titulo, contenido, imagenUrl });
        }

        [Authorize(AuthenticationSchemes = "RedactorCookie,Cookies", Roles = "Redactor,Admin")]
        [HttpGet]
        public IActionResult Editar(int id)
        {
            ViewBag.ActivePage = "noticias";
            var n = _repo.GetNoticiaById(id);
            if (n == null) return NotFound();
            return View(new NuevaNoticiaViewModel
            {
                Titulo        = n.Titulo,
                Contenido     = n.Contenido,
                ImagenPrompt  = n.ImagenUrl,
                GenerarImagen = false
            });
        }

        [Authorize(AuthenticationSchemes = "RedactorCookie,Cookies", Roles = "Redactor,Admin")]
        [HttpPost]
        public async Task<IActionResult> Editar(int id, NuevaNoticiaViewModel vm,
            IFormFile? imagenArchivo)
        {
            if (string.IsNullOrWhiteSpace(vm.Titulo) || string.IsNullOrWhiteSpace(vm.Contenido))
            {
                ViewBag.Error = "El título y el contenido son obligatorios.";
                return View(vm);
            }

            string? imagenUrl = vm.ImagenPrompt; // mantiene la existente si no cambia

            // Si subió un archivo
            if (imagenArchivo != null && imagenArchivo.Length > 0)
            {
                var ext      = Path.GetExtension(imagenArchivo.FileName).ToLower();
                var allowed  = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                if (!allowed.Contains(ext))
                {
                    ViewBag.Error = "Formato de imagen no permitido. Usá JPG, PNG o WebP.";
                    return View(vm);
                }
                var fileName = $"noticia_{id}_{DateTime.Now.Ticks}{ext}";
                var folder   = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "noticias");
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, fileName);
                using var stream = new FileStream(path, FileMode.Create);
                await imagenArchivo.CopyToAsync(stream);
                imagenUrl = $"/img/noticias/{fileName}";
            }
            else if (vm.GenerarImagen && string.IsNullOrEmpty(imagenArchivo?.FileName))
            {
                imagenUrl = _repo.GenerarImagenUrl(vm.Titulo);
            }

            _repo.EditarNoticia(id, vm.Titulo, vm.Contenido, imagenUrl);
            TempData["Mensaje"] = "¡Noticia actualizada!";
            return RedirectToAction("Redaccion");
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
