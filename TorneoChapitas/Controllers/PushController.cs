using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;

namespace TorneoAmigos.Controllers
{
    [Route("api/push")]
    public class PushController : Controller
    {
        private readonly PushService _push;
        public PushController(PushService push) => _push = push;

        /// <summary>Devuelve la VAPID public key para que el cliente pueda suscribirse.</summary>
        [HttpGet("vapid-public-key")]
        public IActionResult GetPublicKey()
        {
            if (!_push.EstaConfigurado)
                return Json(new { ok = false, key = (string?)null });
            return Json(new { ok = true, key = _push.PublicKey });
        }

        /// <summary>Guarda la suscripción de un dispositivo.</summary>
        [HttpPost("suscribir")]
        public IActionResult Suscribir([FromBody] PushSuscripcionDto dto)
        {
            if (string.IsNullOrEmpty(dto?.Endpoint) ||
                string.IsNullOrEmpty(dto?.P256dh)   ||
                string.IsNullOrEmpty(dto?.Auth))
                return Json(new { ok = false, msg = "Datos incompletos." });
            try
            {
                _push.Suscribir(dto.Endpoint, dto.P256dh, dto.Auth);
                return Json(new { ok = true });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, msg = ex.Message });
            }
        }

        /// <summary>Elimina la suscripción de un dispositivo (cuando el usuario revoca permisos).</summary>
        [HttpPost("dessuscribir")]
        public IActionResult Dessuscribir([FromBody] PushEndpointDto dto)
        {
            if (!string.IsNullOrEmpty(dto?.Endpoint))
                _push.Dessuscribir(dto.Endpoint);
            return Json(new { ok = true });
        }
    }

    public class PushSuscripcionDto
    {
        public string Endpoint { get; set; } = "";
        public string P256dh   { get; set; } = "";
        public string Auth     { get; set; } = "";
    }

    public class PushEndpointDto
    {
        public string Endpoint { get; set; } = "";
    }
}
