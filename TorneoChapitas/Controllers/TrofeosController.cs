using CloudinaryDotNet;
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
                    // Subida sin recorte forzado — los trofeos son verticales, no se aplastan
                    var account    = new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret);
                    var cloudinary = new CloudinaryDotNet.Cloudinary(account);
                    cloudinary.Api.Secure = true;

                    using var stream = imagen.OpenReadStream();
                    var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams
                    {
                        File           = new CloudinaryDotNet.FileDescription(imagen.FileName, stream),
                        Folder         = "torneo-chapitas",
                        Transformation = new CloudinaryDotNet.Transformation().Width(600).Crop("limit").Quality("auto")
                    };

                    var result = await cloudinary.UploadAsync(uploadParams);
                    if (result.Error != null)
                        return Json(new { ok = false, msg = "Cloudinary: " + result.Error.Message });

                    url = result.SecureUrl.ToString();
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
