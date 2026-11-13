using MiCaminoBJJ.Web.Filters;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Services;
using MiCaminoBJJ.Web.Services.Admin;
using MiCaminoBJJ.Web.ViewModels;
using MiCaminoBJJ.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MiCaminoBJJ.Web.Controllers;

/// <summary>
/// CRUD administrativo de técnicas (Etapa 3): crear, editar, desactivar y
/// reactivar sobre la base exclusiva SQL Server.
///
/// Seguridad (server-side):
/// - [AdminSoloDevelopment]: fuera de Development todo responde HTTP 404.
/// - [Authorize(Policy = AdministradorContenido)] (Etapa 5): política con
///   DOS esquemas — "Cookies" (admin temporal de Development) e
///   "Identity.Application" (usuario Identity con rol Administrador) — y
///   siempre el rol Administrador. Así un Identity con ese rol SÍ accede al
///   CRUD mientras que una sesión de práctico sigue quedando fuera (403).
///   Se conserva la cookie temporal de Development sin retirarla; la política
///   anterior (AdminLocal, solo "Cookies") quedó demostradamente insuficiente
///   para cuentas Identity administrativas.
/// - [ValidateAntiForgeryToken] en TODOS los POST (+ filtro global).
/// - Sin eliminación física: solo bandera IsActive.
///
/// Consultas (<see cref="IAdminTecnicasQueryService"/>) y comandos
/// (<see cref="IAdminTecnicasCommandService"/>) están separados; las
/// escrituras auditadas son transaccionales en el servicio.
/// </summary>
[AdminSoloDevelopment]
[Authorize(Policy = AdminPolicies.AdministradorContenido)]
public class AdminTecnicasController : Controller
{
    private readonly IAdminTecnicasQueryService _consultas;
    private readonly IAdminTecnicasCommandService _comandos;
    private readonly IAdminTecnicasMediaQueryService _mediaConsultas;
    private readonly ILogger<AdminTecnicasController> _logger;

    public AdminTecnicasController(
        IAdminTecnicasQueryService consultas,
        IAdminTecnicasCommandService comandos,
        IAdminTecnicasMediaQueryService mediaConsultas,
        ILogger<AdminTecnicasController> logger)
    {
        _consultas = consultas ?? throw new ArgumentNullException(nameof(consultas));
        _comandos = comandos ?? throw new ArgumentNullException(nameof(comandos));
        _mediaConsultas = mediaConsultas ?? throw new ArgumentNullException(nameof(mediaConsultas));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Desenlace de un comando ejecutado desde el controlador.</summary>
    private enum AccionResultado
    {
        /// <summary>Rechazo de negocio: re-renderizar el formulario con los errores.</summary>
        ReRender,

        /// <summary>La técnica pedida no existe → HTTP 404.</summary>
        NoEncontrado,

        /// <summary>Éxito o fallo de infraestructura: redirigir con mensaje en TempData.</summary>
        Redirigido
    }

    /// <summary>Actor administrativo autenticado (claim Name de la cookie).</summary>
    private string Actor => User.Identity?.Name ?? DevAdminCredential.DefaultUser;

    // ── Listado ───────────────────────────────────────────────────────────

    /// <summary>
    /// Listado con tres filtros INDEPENDIENTES y combinables entre sí:
    /// búsqueda por nombre/sinónimo, cinturón (catálogo) y estado. Cualquiera
    /// de ellos puede aplicarse solo (p. ej. solo "Violeta" con la búsqueda
    /// vacía). El filtrado ocurre en SQL Server vía <see cref="IAdminTecnicasQueryService"/>.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? buscar = null,
        bool? estado = null,
        int? cinturon = null)
    {
        var tecnicas = await _consultas.ListarAsync(buscar, estado, cinturon);
        var cinturones = await _consultas.ObtenerCinturonesAsync();

        var model = new AdminTecnicasIndexViewModel
        {
            Buscar = buscar,
            Estado = estado,
            Cinturon = cinturon,
            Tecnicas = tecnicas.Select(AdminTecnicaFilaViewModel.Desde).ToList(),
            Cinturones = cinturones.Select(FiltroOpcionViewModel.DesdeBelt).ToList()
        };

        return View(model);
    }

    // ── Creación ──────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Create(
        string? buscar = null,
        bool? estado = null,
        int? cinturon = null)
    {
        var model = new AdminTecnicaFormViewModel
        {
            FiltroBuscar = buscar,
            FiltroEstado = estado,
            FiltroCinturon = cinturon
        };

        await CargarOpcionesAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminTecnicaFormViewModel model)
    {
        // Overposting: el Id NUNCA viene del formulario en la creación (la
        // identidad la asigna SQL Server). El de Edit se fija desde la ruta.
        model.Id = 0;

        if (!ModelState.IsValid)
        {
            await CargarOpcionesAsync(model);
            return View(model);
        }

        var accion = await EjecutarAsync(
            () => _comandos.CrearAsync(AComando(model), Actor),
            "Técnica creada correctamente.");

        return await ContinuarAsync(accion, model);
    }

    // ── Edición ───────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Edit(
        int id,
        string? buscar = null,
        bool? estado = null,
        int? cinturon = null)
    {
        var tecnica = await _consultas.ObtenerAsync(id);
        if (tecnica is null)
            return NotFound();

        var model = new AdminTecnicaFormViewModel
        {
            Id = tecnica.Id,
            Nombre = tecnica.Name,
            Descripcion = tecnica.Description,
            Objetivo = tecnica.Objective,
            Pasos = AdminTecnicaFormViewModel.ATexto(tecnica.Steps),
            Consejos = AdminTecnicaFormViewModel.ATexto(tecnica.Tips),
            CinturonId = tecnica.BeltId,
            CategoriaId = tecnica.CategoryId,
            PosicionId = tecnica.PositionId,
            // Filtros del listado: viajan como ocultos en el form y vuelven al Index.
            FiltroBuscar = buscar,
            FiltroEstado = estado,
            FiltroCinturon = cinturon
        };

        await CargarOpcionesAsync(model);
        await CargarMultimediaAsync(model, buscar, estado, cinturon);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminTecnicaFormViewModel model)
    {
        model.Id = id;

        if (!ModelState.IsValid)
        {
            await CargarOpcionesAsync(model);
            await CargarMultimediaAsync(model, model.FiltroBuscar, model.FiltroEstado, model.FiltroCinturon);
            return View(model);
        }

        var accion = await EjecutarAsync(
            () => _comandos.EditarAsync(id, AComando(model), Actor),
            "Técnica actualizada correctamente.");

        return await ContinuarAsync(accion, model);
    }

    // ── Desactivar / Reactivar (sin eliminación física) ───────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(
        int id,
        string? buscar = null,
        bool? estado = null,
        int? cinturon = null)
    {
        var accion = await EjecutarAsync(
            () => _comandos.DesactivarAsync(id, Actor),
            "Técnica desactivada. Ya no aparece en la biblioteca pública.");

        return accion == AccionResultado.NoEncontrado
            ? NotFound()
            : VolverAlListado(buscar, estado, cinturon);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(
        int id,
        string? buscar = null,
        bool? estado = null,
        int? cinturon = null)
    {
        var accion = await EjecutarAsync(
            () => _comandos.ReactivarAsync(id, Actor),
            "Técnica reactivada. Vuelve a estar visible en la biblioteca pública.");

        return accion == AccionResultado.NoEncontrado
            ? NotFound()
            : VolverAlListado(buscar, estado, cinturon);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Ejecuta un comando administrativo:
    /// - Rechazo de negocio → agrega los errores a ModelState → ReRender.
    /// - Técnica inexistente → NoEncontrado (404).
    /// - Éxito → TempData de éxito → Redirigido.
    /// - Fallo de infraestructura → log + TempData de error → Redirigido
    ///   (el servicio revirtió la transacción: no hay datos parciales).
    /// </summary>
    private async Task<AccionResultado> EjecutarAsync(
        Func<Task<ResultadoOperacion>> comando,
        string mensajeExito)
    {
        try
        {
            var resultado = await comando();

            if (resultado.Ausente)
                return AccionResultado.NoEncontrado;

            if (!resultado.Exito)
            {
                foreach (var error in resultado.Errores)
                    ModelState.AddModelError(string.Empty, error);
                return AccionResultado.ReRender;
            }

            TempData["Exito"] = mensajeExito;
            return AccionResultado.Redirigido;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo administrativo al ejecutar un comando de técnicas.");
            TempData["Error"] = "No se pudo completar la operación. Revisá los datos e intentá nuevamente.";
            return AccionResultado.Redirigido;
        }
    }

    /// <summary>Continuación común de Create/Edit tras ejecutar el comando.</summary>
    private async Task<IActionResult> ContinuarAsync(AccionResultado accion, AdminTecnicaFormViewModel model)
    {
        if (accion == AccionResultado.NoEncontrado)
            return NotFound();

        if (accion == AccionResultado.ReRender)
        {
            await CargarOpcionesAsync(model);
            return View(model);
        }

        // Éxito → vuelvo al listado CON los filtros que traía el formulario.
        return VolverAlListado(model.FiltroBuscar, model.FiltroEstado, model.FiltroCinturon);
    }

    /// <summary>
    /// Redirige al listado administrativo conservando los filtros activos.
    /// Solo se agregan al query string los parámetros con valor, así la URL
    /// limpia (sin filtros) no acumula parámetros vacíos.
    /// </summary>
    private RedirectToActionResult VolverAlListado(string? buscar, bool? estado, int? cinturon)
    {
        var valores = new RouteValueDictionary();

        if (!string.IsNullOrWhiteSpace(buscar))
            valores[AdminTecnicasFiltros.ParamBuscar] = buscar.Trim();
        if (estado.HasValue)
            valores[AdminTecnicasFiltros.ParamEstado] = estado.Value;
        if (cinturon.HasValue)
            valores[AdminTecnicasFiltros.ParamCinturon] = cinturon.Value;

        return RedirectToAction(nameof(Index), valores);
    }

    private static TecnicaComando AComando(AdminTecnicaFormViewModel model) => new(
        model.Id,
        model.Nombre?.Trim() ?? string.Empty,
        model.Descripcion?.Trim() ?? string.Empty,
        model.Objetivo?.Trim() ?? string.Empty,
        AdminTecnicaFormViewModel.AJson(model.Pasos),
        AdminTecnicaFormViewModel.AJson(model.Consejos),
        model.CinturonId ?? 0,
        model.CategoriaId ?? 0,
        model.PosicionId ?? 0);

    private async Task CargarOpcionesAsync(AdminTecnicaFormViewModel model)
    {
        var opciones = await _consultas.ObtenerOpcionesAsync();
        model.Cinturones = opciones.Cinturones;
        model.Categorias = opciones.Categorias;
        model.Posiciones = opciones.Posiciones;
    }

    /// <summary>
    /// Carga la sección «Multimedia de la técnica» para Editar: lista de
    /// recursos + formulario de alta repoblado con el intento fallido (si el
    /// redirect anterior dejó uno en TempData) + filtros del listado.
    /// Un fallo de consulta de multimedia NO tira abajo la edición: se loguea y
    /// la sección muestra estado vacío.
    /// </summary>
    private async Task CargarMultimediaAsync(
        AdminTecnicaFormViewModel model,
        string? buscar,
        bool? estado,
        int? cinturon)
    {
        try
        {
            var recursos = await _mediaConsultas.ObtenerMultimediaAsync(model.Id);

            model.Multimedia = new AdminTecnicaMediaViewModel
            {
                TecnicaId = model.Id,
                Recursos = recursos,
                Alta = new AdminMediaAltaViewModel
                {
                    Url = TempData["MediaUrlIntentada"] as string ?? string.Empty,
                    Titulo = TempData["MediaTituloIntentado"] as string,
                    Descripcion = TempData["MediaDescripcionIntentada"] as string
                },
                FiltroBuscar = buscar,
                FiltroEstado = estado,
                FiltroCinturon = cinturon
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo cargar la multimedia de la técnica {TecnicaId}.", model.Id);
            model.Multimedia = new AdminTecnicaMediaViewModel
            {
                TecnicaId = model.Id,
                Recursos = Array.Empty<AdminMediaRecursoViewModel>(),
                FiltroBuscar = buscar,
                FiltroEstado = estado,
                FiltroCinturon = cinturon
            };
        }
    }
}
