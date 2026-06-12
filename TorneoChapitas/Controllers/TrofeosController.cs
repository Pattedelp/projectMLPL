using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    public class TrofeosController : Controller
    {
        private readonly TemporadaRepository _tempRepo;
        private readonly NoticiasRepository  _noticiasRepo; // reusa Cloudinary
        private readonly IConfiguration      _config;

        public TrofeosController(TemporadaRepository tempRepo, NoticiasRepository noticiasRepo, IConfiguration config)
        {
            _tempRepo    = tempRepo;
            _noticiasRepo = noticiasRepo;
            _config      = config;
        }

        public IActionResult Index()
        {
            ViewBag.ActivePage = "trofeos";
            var trofeos = _tempRepo.GetTrofeos();
            return View(trofeos);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> SubirImagen(int trofeoId, IFormFile imagen)
        {
            if (imagen == null || imagen.Length == 0)
                return Json(new { ok = false, msg = "No se seleccionó imagen" });

            var ext = Path.GetExtension(imagen.FileName).ToLower();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(ext))
                return Json(new { ok = false, msg = "Formato no permitido" });

            try
            {
                var cloudName = _config["CLOUDINARY_CLOUD_NAME"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME");
                var apiKey    = _config["CLOUDINARY_API_KEY"]    ?? Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY");
                var apiSecret = _config["CLOUDINARY_API_SECRET"] ?? Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET");

                string url;
                if (!string.IsNullOrEmpty(cloudName) && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
                {
                    url = await _noticiasRepo.SubirImagenCloudinary(imagen, cloudName, apiKey, apiSecret);
                }
                else
                {
                    using var ms = new MemoryStream();
                    await imagen.CopyToAsync(ms);
                    var mime = ext == ".png" ? "image/png" : ext == ".webp" ? "image/webp" : "image/jpeg";
                    url = $"data:{mime};base64,{Convert.ToBase64String(ms.ToArray())}";
                }

                var ok = _tempRepo.ActualizarImagenTrofeo(trofeoId, url);
                return Json(new { ok, url });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, msg = ex.Message });
            }
        }
    }
}
