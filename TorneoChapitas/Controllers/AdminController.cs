using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly TorneoRepository _repo;
        private readonly TemporadaRepository _tempRepo;

        public AdminController(TorneoRepository repo, TemporadaRepository tempRepo)
        {
            _repo     = repo;
            _tempRepo = tempRepo;
        }

        public IActionResult Index()
        {
            ViewBag.ActivePage = "admin";
            var vm = new HistorialViewModel
            {
                Temporadas      = _tempRepo.GetTodasLasTemporadas(),
                TemporadaActiva = _tempRepo.GetTemporadaActiva()
            };
            return View(vm);
        }

        // ── FINALIZAR TEMPORADA ─────────────────────────
        [HttpPost]
        public IActionResult FinalizarTemporada([FromBody] FinalizarTemporadaDto? dto)
        {
            var temporada = _tempRepo.GetTemporadaActiva();
            if (temporada == null) return Json(new { ok = false, msg = "No hay temporada activa" });

            var tablaPrimera  = _repo.GetTablaPosiciones(1);
            var tablaB        = _repo.GetTablaPosiciones(2);
            bool sinDescensos = dto?.SinDescensos ?? false;

            var ok = _tempRepo.FinalizarTemporada(temporada.Id, tablaPrimera, tablaB, sinDescensos);

            // Registrar títulos de Copa y Supercopa si se indicaron
            if (ok && dto?.CampeonCopaId > 0)
            {
                var tempNom = temporada.Nombre;
                _tempRepo.AgregarTitulo(dto.CampeonCopaId, "campeon_copa", "Campeón Copa Argentina", temporada.Id, tempNom);
            }
            if (ok && dto?.CampeonSupercopaId > 0)
            {
                var tempNom = temporada.Nombre;
                _tempRepo.AgregarTitulo(dto.CampeonSupercopaId, "campeon_supercopa", "Campeón Supercopa Argentina", temporada.Id, tempNom);
            }

            return Json(new { ok, msg = ok ? "Temporada finalizada" : "Error al finalizar" });
        }

        // ── NUEVA TEMPORADA ─────────────────────────────
        [HttpGet]
        public IActionResult NuevaTemporada()
        {
            ViewBag.ActivePage = "admin";
            var equiposPrimera = _repo.GetEquiposByDivision(1);
            var equiposB       = _repo.GetEquiposByDivision(2);
            var temporadas     = _tempRepo.GetTodasLasTemporadas();
            int siguienteNum   = (temporadas.FirstOrDefault()?.Numero ?? 0) + 1;

            var vm = new NuevaTemporadaViewModel
            {
                NumeroTemporada  = siguienteNum,
                EquiposPrimera   = equiposPrimera.Select(e => new EquipoCheckbox
                {
                    Id = e.Id, Nombre = e.Nombre, FlagCode = e.FlagCode,
                    DivisionId = e.DivisionId, Seleccionado = true
                }).ToList(),
                EquiposNacionalB = equiposB.Select(e => new EquipoCheckbox
                {
                    Id = e.Id, Nombre = e.Nombre, FlagCode = e.FlagCode,
                    DivisionId = e.DivisionId, Seleccionado = true
                }).ToList()
            };
            return View(vm);
        }

        [HttpPost]
        public IActionResult NuevaTemporada(
            string nombreTemporada,
            List<int>? equiposPrimera,
            List<int>? equiposB,
            List<string>? nuevosNombres,
            List<int>? nuevasDivisiones)
        {
            equiposPrimera ??= new();
            equiposB       ??= new();

            var nuevos = new List<(string nombre, int divisionId)>();
            if (nuevosNombres != null && nuevasDivisiones != null)
                for (int i = 0; i < Math.Min(nuevosNombres.Count, nuevasDivisiones.Count); i++)
                    if (!string.IsNullOrWhiteSpace(nuevosNombres[i]))
                        nuevos.Add((nuevosNombres[i].Trim(), nuevasDivisiones[i]));

            try
            {
                _tempRepo.CrearNuevaTemporada(nombreTemporada, equiposPrimera, equiposB, nuevos);
                TempData["Mensaje"] = "¡Nueva temporada creada con fixture generado!";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error: " + ex.Message;
                return RedirectToAction("NuevaTemporada");
            }
        }

        // ── COPA ARGENTINA ──────────────────────────────
        [HttpGet]
        public IActionResult SortearCopa()
        {
            ViewBag.ActivePage = "admin";
            var todos = _repo.GetEquiposByDivision(1)
                .Concat(_repo.GetEquiposByDivision(2))
                .Select(e => new EquipoCheckbox
                {
                    Id = e.Id, Nombre = e.Nombre, FlagCode = e.FlagCode,
                    DivisionId = e.DivisionId, Seleccionado = true
                }).ToList();
            return View(todos);
        }

        [HttpPost]
        public IActionResult SortearCopaArgentina([FromBody] SortearCopaDto? dto)
        {
            var temporada = _tempRepo.GetTemporadaActiva();
            if (temporada == null) return Json(new { ok = false, msg = "No hay temporada activa" });

            List<int> equiposPrimera;
            List<int> equiposBOrdenados; // ordenados peor→mejor según tabla
            List<int> equiposNuevos;

            if (dto?.EquiposPrimera != null && dto.EquiposPrimera.Any())
            {
                equiposPrimera = dto.EquiposPrimera;
                var seleccionadosB = dto.EquiposB ?? new List<int>();

                // Tabla de la B para ordenar peor→mejor
                var tablaB = _repo.GetTablaPosiciones(2);
                // IDs con partidos ordenados de peor a mejor
                var conPartidos = tablaB
                    .OrderBy(t => t.Puntos).ThenBy(t => t.DiferenciaGoles)
                    .Select(t => t.EquipoId)
                    .Where(id => seleccionadosB.Contains(id))
                    .ToList();
                // Nuevos = seleccionados que no tienen partidos en tabla
                equiposNuevos = seleccionadosB.Except(conPartidos).ToList();
                // Orden final: nuevos al final (peores), luego peores de tabla
                equiposBOrdenados = conPartidos.Concat(equiposNuevos).ToList();
            }
            else
            {
                equiposPrimera    = _repo.GetEquiposByDivision(1).Select(e => e.Id).ToList();
                var tablaB        = _repo.GetTablaPosiciones(2);
                var conPartidos   = tablaB.OrderBy(t => t.Puntos).ThenBy(t => t.DiferenciaGoles)
                                         .Select(t => t.EquipoId).ToList();
                var todosB        = _repo.GetEquiposByDivision(2).Select(e => e.Id).ToList();
                equiposNuevos     = todosB.Except(conPartidos).ToList();
                equiposBOrdenados = conPartidos.Concat(equiposNuevos).ToList();
            }

            try
            {
                _tempRepo.SortearCopaArgentina(temporada.Id, equiposPrimera, equiposBOrdenados, equiposNuevos);
                return Json(new { ok = true });
            }
            catch (Exception ex) { return Json(new { ok = false, msg = ex.Message }); }
        }

        // ── SUPERCOPA ───────────────────────────────────
        [HttpGet]
        public IActionResult ConfigurarSupercopa()
        {
            ViewBag.ActivePage = "admin";
            return View();
        }

        [HttpPost]
        public IActionResult SortearSupercopa([FromBody] SortearSupercopaDto dto)
        {
            var temporada = _tempRepo.GetTemporadaActiva();
            if (temporada == null) return Json(new { ok = false, msg = "No hay temporada activa" });

            try
            {
                // Lógica: si campeón == campeón copa, agregar semifinal
                bool mismoChampion = dto.CampeonId == dto.CampeonCopaId;
                _tempRepo.SortearSupercopa(temporada.Id, dto.CampeonId, dto.SubcampeonId, dto.CampeonCopaId, mismoChampion);
                return Json(new { ok = true });
            }
            catch (Exception ex) { return Json(new { ok = false, msg = ex.Message }); }
        }

        // ── API helpers para ConfigurarSupercopa ────────
        // ── REGISTRAR TÍTULO COPA ───────────────────────
        [HttpPost]
        public IActionResult RegistrarTituloCopa([FromBody] RegistrarTituloDto dto)
        {
            var temporada = _tempRepo.GetTemporadaActiva()
                ?? _tempRepo.GetTodasLasTemporadas().FirstOrDefault();
            if (temporada == null) return Json(new { ok = false });

            _tempRepo.AgregarTitulo(
                dto.EquipoId,
                dto.TipoTitulo,
                dto.NombreTitulo,
                temporada.Id,
                temporada.Nombre);
            return Json(new { ok = true });
        }

        [HttpGet]
        public IActionResult AgregarJugador()
        {
            ViewBag.ActivePage = "admin";
            return View();
        }

        [HttpGet]
        public IActionResult GetPaisesTomados()
        {
            var todos = _repo.GetEquiposByDivision(1)
                .Concat(_repo.GetEquiposByDivision(2))
                .Where(e => !string.IsNullOrEmpty(e.FlagCode))
                .Select(e => new { equipo = e.Nombre, flagCode = e.FlagCode })
                .ToList();
            return Json(todos);
        }

        [HttpPost]
        public IActionResult AgregarJugador([FromBody] NuevoJugadorDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre)) return Json(new { ok = false, msg = "Nombre requerido" });
            try
            {
                var connStr = HttpContext.RequestServices.GetRequiredService<IConfiguration>().GetConnectionString("TorneoAmigosDB");
                using var conn = new Npgsql.NpgsqlConnection(connStr);
                conn.Open();

                // Verificar si ya existe con ese nombre exacto (jugador retirado que vuelve)
                using var check = new Npgsql.NpgsqlCommand(
                    "SELECT id FROM equipos WHERE LOWER(nombre) = LOWER(@N) LIMIT 1", conn);
                check.Parameters.AddWithValue("@N", dto.Nombre.Trim());
                var existeId = check.ExecuteScalar();

                if (existeId != null && existeId != DBNull.Value)
                {
                    // Jugador ya existe — reactivar y actualizar país y división
                    using var upd = new Npgsql.NpgsqlCommand(
                        "UPDATE equipos SET activo = true, divisionid = @D, pais_code = @P WHERE id = @Id", conn);
                    upd.Parameters.AddWithValue("@D",  dto.DivisionId);
                    upd.Parameters.AddWithValue("@P",  string.IsNullOrEmpty(dto.PaisCode) ? (object)DBNull.Value : dto.PaisCode);
                    upd.Parameters.AddWithValue("@Id", Convert.ToInt32(existeId));
                    upd.ExecuteNonQuery();
                    return Json(new { ok = true, id = Convert.ToInt32(existeId), reactivado = true });
                }
                else
                {
                    // Jugador nuevo — insertar
                    using var cmd = new Npgsql.NpgsqlCommand(
                        "INSERT INTO equipos (divisionid, nombre, colorprincipal, colorsecundario, activo, pais_code) VALUES (@D, @N, '#003366', '#FFD700', true, @P) RETURNING id", conn);
                    cmd.Parameters.AddWithValue("@D", dto.DivisionId);
                    cmd.Parameters.AddWithValue("@N", dto.Nombre.Trim());
                    cmd.Parameters.AddWithValue("@P", string.IsNullOrEmpty(dto.PaisCode) ? (object)DBNull.Value : dto.PaisCode);
                    var id = Convert.ToInt32(cmd.ExecuteScalar());
                    return Json(new { ok = true, id, reactivado = false });
                }
            }
            catch (Exception ex) { return Json(new { ok = false, msg = ex.Message }); }
        }

        [HttpGet]
        public IActionResult GetEquiposPrimera()
        {
            var equipos = _repo.GetEquiposByDivision(1)
                .Select(e => new { id = e.Id, nombre = e.Nombre, flagCode = e.FlagCode });
            return Json(equipos);
        }

        [HttpGet]
        public IActionResult GetEquiposTodos()
        {
            var todos = _repo.GetEquiposByDivision(1)
                .Concat(_repo.GetEquiposByDivision(2))
                .Select(e => new { id = e.Id, nombre = e.Nombre, flagCode = e.FlagCode });
            return Json(todos);
        }
    }

    public class NuevoJugadorDto
    {
        public string Nombre { get; set; } = "";
        public string? PaisCode { get; set; }
        public int DivisionId { get; set; } = 2;
    }

    public class RegistrarTituloDto
    {
        public int EquipoId { get; set; }
        public string TipoTitulo { get; set; } = "";
        public string NombreTitulo { get; set; } = "";
    }

    public class FinalizarTemporadaDto
    {
        public bool SinDescensos { get; set; } = false;
        public int CampeonCopaId { get; set; }
        public int CampeonSupercopaId { get; set; }
    }

    public class SortearCopaDto
    {
        public List<int>? EquiposPrimera { get; set; }
        public List<int>? EquiposB { get; set; }
    }

    public class SortearSupercopaDto
    {
        public int CampeonId { get; set; }
        public int SubcampeonId { get; set; }
        public int CampeonCopaId { get; set; }
    }
}
