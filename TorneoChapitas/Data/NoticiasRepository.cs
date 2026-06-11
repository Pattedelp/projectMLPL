using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using TorneoAmigos.Models;

namespace TorneoAmigos.Data
{
    public class NoticiasRepository
    {
        private readonly string _connectionString;

        public NoticiasRepository(IConfiguration cfg) =>
            _connectionString = cfg.GetConnectionString("TorneoAmigosDB")
                ?? throw new InvalidOperationException("Connection string not found.");

        private NpgsqlConnection GetConnection() => new(_connectionString);

        // ── NOTICIAS ────────────────────────────────────

        public List<Noticia> GetNoticias(bool soloPublicadas = true)
        {
            var lista = new List<Noticia>();
            var where = soloPublicadas ? "WHERE publicada = true" : "";
            // Con Cloudinary las URLs son pequeñas — podemos traerlas directamente
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                $"SELECT id, titulo, contenido, imagen_url, tipo, autor, publicada, created_at FROM noticias {where} ORDER BY created_at DESC", conn);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read()) lista.Add(MapNoticia(r));
            return lista;
        }

        public Noticia? GetNoticiaById(int id)
        {
            // Acá SÍ traemos imagen_url completa (solo para el detalle)
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                "SELECT id, titulo, contenido, imagen_url, tipo, autor, publicada, created_at FROM noticias WHERE id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            using var r = cmd.ExecuteReader();
            return r.Read() ? MapNoticia(r) : null;
        }

        public string? GetImagenUrl(int id)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                "SELECT imagen_url FROM noticias WHERE id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            var result = cmd.ExecuteScalar();
            return result == DBNull.Value ? null : result as string;
        }

        public int CrearNoticia(string titulo, string contenido, string? imagenUrl, string tipo, string autor)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(@"
                INSERT INTO noticias (titulo, contenido, imagen_url, tipo, autor, publicada)
                VALUES (@T, @C, @I, @Ti, @A, true) RETURNING id", conn);
            cmd.Parameters.AddWithValue("@T",  titulo);
            cmd.Parameters.AddWithValue("@C",  contenido);
            cmd.Parameters.AddWithValue("@I",  (object?)imagenUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Ti", tipo);
            cmd.Parameters.AddWithValue("@A",  autor);
            conn.Open();
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public bool EliminarNoticia(int id)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand("DELETE FROM noticias WHERE id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool TogglePublicada(int id)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                "UPDATE noticias SET publicada = NOT publicada WHERE id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        // ── USUARIOS REDACTORES ─────────────────────────

        public UsuarioRedactor? ValidarLogin(string username, string password)
        {
            var hash = HashPassword(password);
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                "SELECT id, username, rol, activo FROM usuarios WHERE username = @U AND password_hash = @P AND activo = true", conn);
            cmd.Parameters.AddWithValue("@U", username);
            cmd.Parameters.AddWithValue("@P", hash);
            conn.Open();
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new UsuarioRedactor
            {
                Id = r.GetInt32(0), Username = r.GetString(1),
                Rol = r.GetString(2), Activo = r.GetBoolean(3)
            };
        }

        public bool CrearUsuario(string username, string password, string rol = "redactor")
        {
            var hash = HashPassword(password);
            try
            {
                using var conn = GetConnection();
                using var cmd  = new NpgsqlCommand(
                    "INSERT INTO usuarios (username, password_hash, rol) VALUES (@U, @P, @R)", conn);
                cmd.Parameters.AddWithValue("@U", username);
                cmd.Parameters.AddWithValue("@P", hash);
                cmd.Parameters.AddWithValue("@R", rol);
                conn.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
            catch { return false; }
        }

        public bool CambiarPassword(string username, string nuevaPassword)
        {
            var hash = HashPassword(nuevaPassword);
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                "UPDATE usuarios SET password_hash = @P WHERE username = @U", conn);
            cmd.Parameters.AddWithValue("@P", hash);
            cmd.Parameters.AddWithValue("@U", username);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        public List<UsuarioRedactor> GetUsuarios()
        {
            var lista = new List<UsuarioRedactor>();
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(
                "SELECT id, username, rol, activo FROM usuarios ORDER BY username", conn);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                lista.Add(new UsuarioRedactor
                {
                    Id = r.GetInt32(0), Username = r.GetString(1),
                    Rol = r.GetString(2), Activo = r.GetBoolean(3)
                });
            return lista;
        }

        // ── GENERADOR DE NOTICIAS AUTOMÁTICAS ──────────

        public string GenerarTextoNoticia(
            List<PosicionViewModel> tablaPrimera,
            List<PosicionViewModel> tablaB,
            string tipoNoticia = "tabla")
        {
            return tipoNoticia switch
            {
                "tabla"    => GenerarNoticiaTablaPosiciones(tablaPrimera, tablaB),
                "lider"    => GenerarNoticiaLider(tablaPrimera),
                "descenso" => GenerarNoticiaDescenso(tablaPrimera),
                _          => GenerarNoticiaTablaPosiciones(tablaPrimera, tablaB)
            };
        }

        private string GenerarNoticiaTablaPosiciones(
            List<PosicionViewModel> primera,
            List<PosicionViewModel> b)
        {
            if (!primera.Any()) return "El torneo está por comenzar. ¡Pronto habrá novedades!";

            var lider      = primera[0];
            var segundo    = primera.Count > 1 ? primera[1] : null;
            var tercero    = primera.Count > 2 ? primera[2] : null;
            var descenso1  = primera.Count >= 1 ? primera[primera.Count - 1] : null;
            var descenso2  = primera.Count >= 2 ? primera[primera.Count - 2] : null;
            var liderB     = b.FirstOrDefault();

            var diferencia = segundo != null ? lider.Puntos - segundo.Puntos : 0;
            var fechasRest = 9 - (lider.PartidosJugados);

            var frasesDiferencia = diferencia switch
            {
                0     => $"está empatado en puntos con {segundo?.NombreEquipo}",
                1     => $"aventaja por solo 1 punto a {segundo?.NombreEquipo}",
                2 or 3 => $"tiene {diferencia} puntos de ventaja sobre {segundo?.NombreEquipo}",
                _     => $"se escapa con {diferencia} puntos de diferencia sobre {segundo?.NombreEquipo}"
            };

            var intro = lider.PartidosJugados == 0
                ? "El torneo acaba de comenzar y todos los equipos tienen todo por delante."
                : $"Jornada de emociones en la Primera División del Torneo Chapitas.";

            var cuerpo = $"{lider.NombreEquipo} lidera la tabla con {lider.Puntos} puntos y {frasesDiferencia}.";

            var terceroTexto = tercero != null
                ? $" {tercero.NombreEquipo} completa el podio desde la tercera posición."
                : "";

            var descensoTexto = "";
            if (descenso1 != null && lider.PartidosJugados > 2)
                descensoTexto = $" En la zona baja, {descenso2?.NombreEquipo} y {descenso1.NombreEquipo} pelean por salir de los puestos de descenso.";

            var bTexto = liderB != null && liderB.PartidosJugados > 0
                ? $" En la Primera Nacional, {liderB.NombreEquipo} comanda las posiciones con {liderB.Puntos} puntos."
                : "";

            var cierre = fechasRest > 0
                ? $" Quedan {fechasRest} fechas para definir el campeón. ¡Todo puede pasar!"
                : " ¡El torneo llega a su recta final!";

            return $"{intro} {cuerpo}{terceroTexto}{descensoTexto}{bTexto}{cierre}";
        }

        private string GenerarNoticiaLider(List<PosicionViewModel> tabla)
        {
            if (!tabla.Any()) return "";
            var lider   = tabla[0];
            var segundo = tabla.Count > 1 ? tabla[1] : null;
            var ventaja = segundo != null ? lider.Puntos - segundo.Puntos : lider.Puntos;

            var frases = new[]
            {
                $"{lider.NombreEquipo} sigue siendo el rey de la Primera División.",
                $"Nadie puede con {lider.NombreEquipo} en lo más alto de la tabla.",
                $"{lider.NombreEquipo} no afloja y se mantiene firme en la cima del Torneo Chapitas."
            };
            var frase = frases[lider.PartidosJugados % frases.Length];

            var ventajaTexto = ventaja > 0 && segundo != null
                ? $" Con {ventaja} punto{(ventaja > 1 ? "s" : "")} sobre {segundo.NombreEquipo}, el camino hacia el título parece despejado."
                : "";

            var stats = $" Lleva {lider.Ganados} victorias, {lider.Perdidos} derrotas y {lider.GolesAFavor} goles a favor en {lider.PartidosJugados} partidos jugados.";

            return frase + ventajaTexto + stats;
        }

        private string GenerarNoticiaDescenso(List<PosicionViewModel> tabla)
        {
            if (tabla.Count < 2) return "";
            var ultimo      = tabla[tabla.Count - 1];
            var penultimo   = tabla[tabla.Count - 2];
            var salvado     = tabla.Count >= 3 ? tabla[tabla.Count - 3] : null;
            var diferencia  = salvado != null ? salvado.Puntos - ultimo.Puntos : 0;

            return $"La zona de descenso se calienta en el Torneo Chapitas. " +
                   $"{penultimo.NombreEquipo} y {ultimo.NombreEquipo} están en los puestos de descenso a la Primera Nacional. " +
                   $"{(salvado != null ? $"{salvado.NombreEquipo} está a salvo pero con apenas {diferencia} punto{(diferencia > 1 ? "s" : "")} de ventaja. " : "")}" +
                   $"Cada partido es una final para estos equipos.";
        }

        public string GenerarTituloNoticia(List<PosicionViewModel> tabla, string tipo)
        {
            if (!tabla.Any()) return "Actualización del Torneo Chapitas";
            var lider = tabla[0];

            return tipo switch
            {
                "lider"    => $"{lider.NombreEquipo} sigue en lo más alto",
                "descenso" => "Tensión en la zona baja: la lucha por la permanencia",
                "tabla"    => $"Así está la tabla: {lider.NombreEquipo} lidera con {lider.Puntos} puntos",
                _          => "Resumen del Torneo Chapitas"
            };
        }

        public string GenerarImagenUrl(string contexto)
        {
            var seed = Math.Abs(contexto.GetHashCode()) % 1000;
            return $"https://source.unsplash.com/800x450/?football,soccer,stadium&sig={seed}";
        }

        public async Task<string> SubirImagenCloudinary(IFormFile archivo, string cloudName, string apiKey, string apiSecret)
        {
            var account = new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret);
            var cloudinary = new CloudinaryDotNet.Cloudinary(account);
            cloudinary.Api.Secure = true;

            using var stream = archivo.OpenReadStream();
            var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams
            {
                File           = new CloudinaryDotNet.FileDescription(archivo.FileName, stream),
                Folder         = "torneo-chapitas",
                Transformation = new CloudinaryDotNet.Transformation()
                                    .Width(900).Height(500).Crop("fill").Quality("auto")
            };

            var result = await cloudinary.UploadAsync(uploadParams);
            if (result.Error != null)
                throw new Exception("Cloudinary error: " + result.Error.Message);

            return result.SecureUrl.ToString();
        }

        public async Task<string> GenerarImagenUrlIA(string contexto, string? stabilityKey)
        {
            // Si no hay key, usar Unsplash directamente
            if (string.IsNullOrEmpty(stabilityKey))
            {
                Console.WriteLine("[Noticias] No hay STABILITY_API_KEY, usando Unsplash");
                return GenerarImagenUrl(contexto);
            }

            Console.WriteLine($"[Noticias] Intentando Stability AI para: {contexto}");

            try
            {
                using var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {stabilityKey}");
                http.Timeout = TimeSpan.FromSeconds(45);

                var prompt = $"dramatic soccer football match, {contexto}, stadium lights, cinematic, dark blue and gold colors, argentina football atmosphere, high quality";

                var body = new
                {
                    text_prompts = new[] { new { text = prompt, weight = 1.0 } },
                    cfg_scale    = 7,
                    height       = 512,
                    width        = 896,
                    steps        = 30,
                    samples      = 1
                };

                var json     = System.Text.Json.JsonSerializer.Serialize(body);
                var response = await http.PostAsync(
                    "https://api.stability.ai/v1/generation/stable-diffusion-xl-1024-v1-0/text-to-image",
                    new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                    return GenerarImagenUrl(contexto); // fallback a Unsplash

                var result  = await response.Content.ReadAsStringAsync();
                var doc     = System.Text.Json.JsonDocument.Parse(result);
                var base64  = doc.RootElement
                    .GetProperty("artifacts")[0]
                    .GetProperty("base64").GetString();

                if (string.IsNullOrEmpty(base64))
                    return GenerarImagenUrl(contexto);

                // Guardar imagen localmente
                var bytes    = Convert.FromBase64String(base64);
                var fileName = $"ia_{DateTime.Now.Ticks}.png";
                var folder   = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "noticias");
                Directory.CreateDirectory(folder);
                await File.WriteAllBytesAsync(Path.Combine(folder, fileName), bytes);
                return $"/img/noticias/{fileName}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Noticias] Error Stability AI: {ex.Message} - usando Unsplash como fallback");
                return GenerarImagenUrl(contexto);
            }
        }

        public bool EditarNoticia(int id, string titulo, string contenido, string? imagenUrl)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(@"
                UPDATE noticias SET titulo=@T, contenido=@C, imagen_url=@I WHERE id=@Id", conn);
            cmd.Parameters.AddWithValue("@T",  titulo);
            cmd.Parameters.AddWithValue("@C",  contenido);
            cmd.Parameters.AddWithValue("@I",  (object?)imagenUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        // ── HELPERS ─────────────────────────────────────

        public static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password + "torneochapitas_salt"));
            return Convert.ToHexString(bytes).ToLower();
        }

        private static Noticia MapNoticia(IDataReader r) => new()
        {
            Id        = r.GetInt32(0),
            Titulo    = r.GetString(1),
            Contenido = r.GetString(2),
            ImagenUrl = r.IsDBNull(3) ? null : r.GetString(3),
            Tipo      = r.GetString(4),
            Autor     = r.GetString(5),
            Publicada = r.GetBoolean(6),
            CreatedAt = r.GetDateTime(7)
        };
    }
}
