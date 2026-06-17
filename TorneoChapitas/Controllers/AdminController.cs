using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TorneoAmigos.Data;
using TorneoAmigos.Models;

namespace TorneoAmigos.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly TorneoRepository    _repo;
        private readonly TemporadaRepository  _tempRepo;
        private readonly EncuestasRepository  _encuestas;

        public AdminController(TorneoRepository repo, TemporadaRepository tempRepo, EncuestasRepository encuestas)
        {
            _repo      = repo;
            _tempRepo  = tempRepo;
            _encuestas = encuestas;
        }

        public IActionResult Index()
        {
            ViewBag.ActivePage = "admin";
            var temporadaActiva = _tempRepo.GetTemporadaActiva();
            var vm = new HistorialViewModel
            {
                Temporadas      = _tempRepo.GetTodasLasTemporadas(),
                TemporadaActiva = temporadaActiva
            };
            if (temporadaActiva != null)
            {
                ViewBag.Cierre = _tempRepo.GetCierre(temporadaActiva.Id);
                var tablaB = _repo.GetTablaPosiciones(2);
                var equiposB = _repo.GetEquiposByDivision(2);
                ViewBag.TablaBReducido = tablaB.Count >= 6 ? tablaB : equiposB.Select((e, i) => new PosicionViewModel
                {
                    EquipoId = e.Id, NombreEquipo = e.Nombre, FlagCode = e.FlagCode, Posicion = i + 1
                }).ToList();
                ViewBag.TablaPrimeraReducido = _repo.GetTablaPosiciones(1);
                ViewBag.TodosJugadosB = tablaB.Count >= 6 && _tempRepo.TodosLosPartidosRegularesJugados(2);
            }
            ViewBag.EquiposTodos = _repo.GetEquiposByDivision(1)
                .Concat(_repo.GetEquiposByDivision(2))
                .OrderBy(e => e.Nombre).ToList();
            ViewBag.EquiposB = _repo.GetEquiposByDivision(2).ToList();
            ViewBag.TodasEncuestas = _encuestas.GetTodasLasEncuestas();
            return View(vm);
        }

        // ── REDUCIDO ─────────────────────────────────────
        [HttpPost]
        public IActionResult GenerarReducido()
        {
            var tablaB = _repo.GetTablaPosiciones(2);
            var (ok, msg) = _tempRepo.GenerarReducido(tablaB);
            return Json(new { ok, msg });
        }

        [HttpPost]
        public IActionResult GenerarFinalReducido([FromBody] FinalReducidoDto dto)
        {
            // Agrega la final del reducido entre los dos ganadores de semis
            var ok = _tempRepo.GenerarFinalOPromocion(dto.Local, dto.Visitante, "reducido_final");
            return Json(new { ok });
        }

        [HttpPost]
        public IActionResult GenerarPromocion([FromBody] FinalReducidoDto dto)
        {
            // Agrega el partido de promoción: ganador reducido vs 8° de Primera
            var ok = _tempRepo.GenerarFinalOPromocion(dto.Local, dto.Visitante, "promocion");
            return Json(new { ok });
        }

        // ── REGENERAR FIXTURE ───────────────────────────
        [HttpPost]
        public IActionResult RegenerarFixture()
        {
            var ok = _tempRepo.RegenerarFixture();
            return Json(new { ok, msg = ok ? "Fixture regenerado con los equipos actuales." : "No se puede regenerar: ya hay partidos jugados." });
        }

        // ── BORRAR FIXTURE ──────────────────────────────
        [HttpPost]
        public IActionResult BorrarFixture()
        {
            var temporada = _tempRepo.GetTemporadaActiva();
            if (temporada == null) return Json(new { ok = false, msg = "No hay temporada activa" });
            var ok = _tempRepo.BorrarFixtureSinResultados(temporada.Id);
            return Json(new { ok, msg = ok ? "Fixture borrado. Podés generar uno nuevo desde Nueva Temporada." : "No se puede borrar: ya hay partidos jugados." });
        }

        // ── CONFIGURACIÓN DE TEMPORADA ───────────────────
        [HttpPost]
        public IActionResult ActualizarConfigTemporada([FromBody] ConfigTemporadaDto dto)
        {
            var temporada = _tempRepo.GetTemporadaActiva();
            if (temporada == null) return Json(new { ok = false });
            var ok = _tempRepo.ActualizarConfigTemporada(temporada.Id,
                dto.CantDescensos, dto.CantAscensos, dto.TienePromocion,
                dto.TienePromocion ? dto.PosPromocionPrimera : null,
                dto.TienePromocion ? dto.PosPromocionB : null);
            return Json(new { ok });
        }

        // ── GUARDAR BORRADOR CIERRE ─────────────────────
        [HttpPost]
        public IActionResult GuardarCierre([FromBody] GuardarCierreDto dto)
        {
            var temporada = _tempRepo.GetTemporadaActiva();
            if (temporada == null) return Json(new { ok = false, msg = "No hay temporada activa" });

            var cierre = new TemporadaCierre
            {
                TemporadaId        = temporada.Id,
                CampeonCopaId      = dto.CampeonCopaId      > 0 ? dto.CampeonCopaId      : null,
                CampeonSupercopaId = dto.CampeonSupercopaId > 0 ? dto.CampeonSupercopaId : null,
                CampeonPrimeraId   = dto.CampeonPrimeraId   > 0 ? dto.CampeonPrimeraId   : null,
                CampeonBId         = dto.CampeonBId         > 0 ? dto.CampeonBId         : null,
                Ascenso1Id         = dto.Ascenso1Id         > 0 ? dto.Ascenso1Id         : null,
                Ascenso2Id         = dto.Ascenso2Id         > 0 ? dto.Ascenso2Id         : null,
                Descenso1Id        = dto.Descenso1Id        > 0 ? dto.Descenso1Id        : null,
                Descenso2Id        = dto.Descenso2Id        > 0 ? dto.Descenso2Id        : null,
                SinDescensos       = dto.SinDescensos
            };

            var ok = _tempRepo.GuardarCierre(cierre);

            // Registrar en palmarés — primero borra el anterior del mismo tipo/temporada, luego inserta
            if (ok)
            {
                var tempNom = temporada.Nombre;
                void ActualizarCampeon(int? nuevoId, string tipo, string nombre)
                {
                    if (!nuevoId.HasValue) return;
                    // Borrar campeón anterior del mismo tipo en esta temporada
                    _tempRepo.BorrarTitulo(tipo, temporada.Id);
                    // Insertar el nuevo
                    _tempRepo.AgregarTitulo(nuevoId.Value, tipo, nombre, temporada.Id, tempNom);
                }
                ActualizarCampeon(cierre.CampeonCopaId,      "campeon_copa",       "Campeón Copa Argentina");
                ActualizarCampeon(cierre.CampeonSupercopaId, "campeon_supercopa",  "Campeón Supercopa Argentina");
                ActualizarCampeon(cierre.CampeonPrimeraId,   "campeon_torneo",     "Campeón Primera División");
                ActualizarCampeon(cierre.CampeonBId,         "campeon_primera_b",  "Campeón Primera Nacional");
            }

            return Json(new { ok });
        }

        // ── FINALIZAR TEMPORADA ─────────────────────────
        [HttpPost]
        public IActionResult FinalizarTemporada([FromBody] FinalizarTemporadaDto? dto)
        {
            var temporada = _tempRepo.GetTemporadaActiva();
            if (temporada == null) return Json(new { ok = false, msg = "No hay temporada activa" });

            // Usar borrador guardado, con fallback al DTO por compatibilidad
            var cierre = _tempRepo.GetCierre(temporada.Id);

            var tablaPrimera  = _repo.GetTablaPosiciones(1);
            var tablaB        = _repo.GetTablaPosiciones(2);
            bool sinDescensos = cierre?.SinDescensos ?? dto?.SinDescensos ?? false;

            // Aplicar ascensos/descensos manuales si están en el borrador
            if (cierre != null)
            {
                // Reordenar tablas según borrador si hay movimientos manuales
                if (cierre.Ascenso1Id.HasValue || cierre.Descenso1Id.HasValue)
                {
                    // Los movimientos manuales se aplican directamente en FinalizarTemporada
                }
            }

            var ok = _tempRepo.FinalizarTemporada(temporada.Id, tablaPrimera, tablaB, sinDescensos, cierre);

            // Registrar títulos (usar borrador primero, fallback a DTO)
            var tempNom = temporada.Nombre;
            var copaId      = cierre?.CampeonCopaId      ?? (dto?.CampeonCopaId      > 0 ? dto.CampeonCopaId : 0);
            var primeraId   = cierre?.CampeonPrimeraId   ?? 0;
            var bId         = cierre?.CampeonBId         ?? 0;
            var supercopaId = cierre?.CampeonSupercopaId ?? (dto?.CampeonSupercopaId > 0 ? dto.CampeonSupercopaId : 0);

            if (ok && copaId > 0)
                _tempRepo.AgregarTitulo(copaId, "campeon_copa", "Campeón Copa Argentina", temporada.Id, tempNom);
            if (ok && supercopaId > 0)
                _tempRepo.AgregarTitulo(supercopaId, "campeon_supercopa", "Campeón Supercopa Argentina", temporada.Id, tempNom);

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

            // Calcular el siguiente número considerando el historial legacy
            // El historial tiene T4-T16 en enfrentamientos_historicos
            // Tomamos el máximo entre las temporadas del sistema y 16 (último número conocido del legacy)
            int maxSistema = temporadas.Any() ? temporadas.Max(t => t.Numero) : 0;
            int maxLegacy  = _tempRepo.GetMaxNumeroTemporadaLegacy();
            int siguienteNum = Math.Max(maxSistema, maxLegacy) + 1;

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
            List<int>? nuevasDivisiones,
            int cantDescensos = 2,
            int cantAscensos = 2,
            bool tienePromocion = false,
            int posPromocionPrimera = 8,
            int posPromocionB = 3)
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
                _tempRepo.CrearNuevaTemporada(nombreTemporada, equiposPrimera, equiposB, nuevos,
                    cantDescensos, cantAscensos, tienePromocion,
                    tienePromocion ? posPromocionPrimera : null,
                    tienePromocion ? posPromocionB : null);
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

    public class FinalReducidoDto
    {
        public int Local { get; set; }
        public int Visitante { get; set; }
    }

    public class ConfigTemporadaDto
    {
        public int CantDescensos { get; set; } = 2;
        public int CantAscensos { get; set; } = 2;
        public bool TienePromocion { get; set; } = false;
        public int PosPromocionPrimera { get; set; } = 8;
        public int PosPromocionB { get; set; } = 3;
    }

    public class GuardarCierreDto
    {
        public int CampeonCopaId { get; set; }
        public int CampeonSupercopaId { get; set; }
        public int Ascenso1Id { get; set; }
        public int Ascenso2Id { get; set; }
        public int Descenso1Id { get; set; }
        public int Descenso2Id { get; set; }
        public int CampeonPrimeraId { get; set; }
        public int CampeonBId { get; set; }
        public bool SinDescensos { get; set; }
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
