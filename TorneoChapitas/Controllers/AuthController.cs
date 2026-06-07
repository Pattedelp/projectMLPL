using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace TorneoAmigos.Controllers
{
    public class AuthController : Controller
    {
        private readonly IConfiguration _cfg;
        public AuthController(IConfiguration cfg) => _cfg = cfg;

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string usuario, string password, string? returnUrl = null)
        {
            var adminUser = _cfg["Admin:Usuario"] ?? "admin";
            var adminPass = _cfg["Admin:Password"] ?? "chapitas2025";

            if (usuario == adminUser && password == adminPass)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, usuario),
                    new Claim(ClaimTypes.Role, "Admin")
                };
                var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc   = DateTimeOffset.UtcNow.AddDays(7)
                    });

                return Redirect(returnUrl ?? "/");
            }

            ViewBag.Error     = "Usuario o contraseña incorrectos.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}
