using Npgsql;
using WebPush;

namespace TorneoAmigos.Data
{
    /// <summary>
    /// Servicio de notificaciones push (Web Push / VAPID).
    /// Requiere el paquete NuGet: WebPush (Minelli)
    /// Agregar en TorneoChapitas.csproj:
    ///   <PackageReference Include="WebPush" Version="1.0.11" />
    /// </summary>
    public class PushService
    {
        private readonly string _connectionString;
        private readonly string? _publicKey;
        private readonly string? _privateKey;
        private readonly string  _subject;

        public PushService(IConfiguration cfg)
        {
            _connectionString = cfg.GetConnectionString("TorneoAmigosDB")
                ?? throw new InvalidOperationException("Connection string not found.");
            _publicKey  = cfg["VAPID_PUBLIC_KEY"]  ?? Environment.GetEnvironmentVariable("VAPID_PUBLIC_KEY");
            _privateKey = cfg["VAPID_PRIVATE_KEY"] ?? Environment.GetEnvironmentVariable("VAPID_PRIVATE_KEY");
            _subject    = "mailto:admin@torneochapitas.com";
        }

        private NpgsqlConnection GetConnection() => new(_connectionString);

        public bool EstaConfigurado => !string.IsNullOrEmpty(_publicKey) && !string.IsNullOrEmpty(_privateKey);
        public string? PublicKey => _publicKey;

        // ── GUARDAR SUSCRIPCIÓN ──────────────────────────────────────
        public void Suscribir(string endpoint, string p256dh, string auth)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(@"
                INSERT INTO push_subscriptions (endpoint, p256dh, auth)
                VALUES (@E, @P, @A)
                ON CONFLICT (endpoint) DO UPDATE SET p256dh = @P, auth = @A", conn);
            cmd.Parameters.AddWithValue("@E", endpoint);
            cmd.Parameters.AddWithValue("@P", p256dh);
            cmd.Parameters.AddWithValue("@A", auth);
            conn.Open();
            cmd.ExecuteNonQuery();
        }

        public void Dessuscribir(string endpoint)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                "DELETE FROM push_subscriptions WHERE endpoint = @E", conn);
            cmd.Parameters.AddWithValue("@E", endpoint);
            conn.Open();
            cmd.ExecuteNonQuery();
        }

        // ── ENVIAR NOTIFICACIÓN A TODOS ──────────────────────────────
        public async Task EnviarATodosAsync(string titulo, string cuerpo, string? url = null)
        {
            if (!EstaConfigurado)
            {
                Console.WriteLine("[Push] VAPID keys no configuradas — notificación omitida.");
                return;
            }

            var suscripciones = GetTodasLasSuscripciones();
            if (!suscripciones.Any()) return;

            var vapidDetails = new VapidDetails(_subject, _publicKey!, _privateKey!);
            var client       = new WebPushClient();

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                titulo,
                cuerpo,
                url = url ?? "/Noticias"
            });

            var tareas = suscripciones.Select(async s =>
            {
                try
                {
                    var sub = new PushSubscription(s.Endpoint, s.P256dh, s.Auth);
                    await client.SendNotificationAsync(sub, payload, vapidDetails);
                }
                catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone
                                                || ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Suscripción expirada o inválida — limpiar
                    Dessuscribir(s.Endpoint);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Push] Error enviando a {s.Endpoint[..Math.Min(40, s.Endpoint.Length)]}...: {ex.Message}");
                }
            });

            await Task.WhenAll(tareas);
            Console.WriteLine($"[Push] Notificación enviada a {suscripciones.Count} dispositivos.");
        }

        // ── LEER SUSCRIPCIONES ───────────────────────────────────────
        private List<(string Endpoint, string P256dh, string Auth)> GetTodasLasSuscripciones()
        {
            var lista = new List<(string, string, string)>();
            try
            {
                using var conn = GetConnection();
                using var cmd  = new NpgsqlCommand(
                    "SELECT endpoint, p256dh, auth FROM push_subscriptions", conn);
                conn.Open();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    lista.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Push] Error leyendo suscripciones: {ex.Message}");
            }
            return lista;
        }
    }
}
