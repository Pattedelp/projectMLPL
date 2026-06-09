using System.Data;
using Npgsql;
using TorneoAmigos.Models;

namespace TorneoAmigos.Data
{
    public class ComentariosRepository
    {
        private readonly string _connectionString;

        public ComentariosRepository(IConfiguration cfg) =>
            _connectionString = cfg.GetConnectionString("TorneoAmigosDB")
                ?? throw new InvalidOperationException("Connection string not found.");

        private NpgsqlConnection GetConnection() => new(_connectionString);

        // ── COMENTARIOS ─────────────────────────────────

        public List<Comentario> GetComentarios(int noticiaId)
        {
            var lista = new List<Comentario>();
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(@"
                SELECT id, noticia_id, autor, pais_flag, contenido, aprobado, created_at
                FROM comentarios
                WHERE noticia_id = @N AND aprobado = true
                ORDER BY created_at ASC", conn);
            cmd.Parameters.AddWithValue("@N", noticiaId);
            conn.Open();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                lista.Add(new Comentario
                {
                    Id        = r.GetInt32(0),
                    NoticiaId = r.GetInt32(1),
                    Autor     = r.GetString(2),
                    PaisFlag  = r.IsDBNull(3) ? null : r.GetString(3),
                    Contenido = r.GetString(4),
                    Aprobado  = r.GetBoolean(5),
                    CreatedAt = r.GetDateTime(6)
                });
            return lista;
        }

        public bool AgregarComentario(int noticiaId, string autor, string? paisFlag, string contenido)
        {
            if (string.IsNullOrWhiteSpace(contenido) || contenido.Length > 500) return false;
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand(@"
                INSERT INTO comentarios (noticia_id, autor, pais_flag, contenido, aprobado)
                VALUES (@N, @A, @F, @C, true)", conn);
            cmd.Parameters.AddWithValue("@N", noticiaId);
            cmd.Parameters.AddWithValue("@A", autor.Length > 50 ? autor[..50] : autor);
            cmd.Parameters.AddWithValue("@F", (object?)paisFlag ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@C", contenido);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        public bool EliminarComentario(int id)
        {
            using var conn = GetConnection();
            using var cmd  = new NpgsqlCommand("DELETE FROM comentarios WHERE id = @Id", conn);
            cmd.Parameters.AddWithValue("@Id", id);
            conn.Open();
            return cmd.ExecuteNonQuery() > 0;
        }

        // ── USUARIOS ONLINE ──────────────────────────────

        public int GetUsuariosOnline()
        {
            try
            {
                using var conn = GetConnection();
                // Limpiar viejos y contar activos en una sola conexión
                using var cmd = new NpgsqlCommand(@"
                    DELETE FROM usuarios_online WHERE ultima_actividad < NOW() - INTERVAL '2 minutes';
                    SELECT COUNT(*) FROM usuarios_online;", conn);
                conn.Open();
                cmd.ExecuteNonQuery();
                // Segunda query
                using var cmd2 = new NpgsqlCommand("SELECT COUNT(*) FROM usuarios_online", conn);
                return Convert.ToInt32(cmd2.ExecuteScalar());
            }
            catch { return 0; }
        }

        public void PingUsuario(string sessionId)
        {
            try
            {
                using var conn = GetConnection();
                using var cmd  = new NpgsqlCommand(@"
                    INSERT INTO usuarios_online (session_id, ultima_actividad)
                    VALUES (@S, NOW())
                    ON CONFLICT (session_id) DO UPDATE SET ultima_actividad = NOW()", conn);
                cmd.Parameters.AddWithValue("@S", sessionId);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
            catch { }
        }
    }
}
