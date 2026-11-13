using MiCaminoBJJ.Web.Filters;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Services.Admin;
using MiCaminoBJJ.Web.ViewModels;
using MiCaminoBJJ.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MiCaminoBJJ.Web.Controllers;

/// <summary>
/// Acciones administrativas DEDICADAS de la sección «Multimedia de la
/// técnica» (requisito: gestionar multimedia sin alterar el formulario
/// principal de Editar).
///
/// Seguridad (idéntica al CRUD de técnicas):
/// - [AdminSoloDevelopment]: fuera de Development todo responde HTTP 404.
/// - [Authorize(Policy = AdministradorContenido)]: siempre el rol
///   Administrador (cookies de Development o Identity).
/// - [ValidateAntiForgeryToken] en TODOS los POST (+ filtro global).
///
/// Flujo: cada acción valida en servidor (URL/formato/límites en
/// <see cref="AdminTecnicasMediaCommandService"/>), auditA y redirige a
/// AdminTecnicas/Edit/{id} con los filtros del listado conservados y mensaje
/// en TempData. NUNCA se descarga video de YouTube: solo se persisten URL,
/// id de video y proveedor.
///
/// Desvincular (Quitar) elimina SOLO la asociación: el MediaAsset (archivo o
/// recurso compartido) se conserva íntegro por diseño.
/// </summary>
[AdminSoloDevelopment]
[Authorize(Policy = AdminPolicies.AdministradorContenido)]
public class AdminTecnicasMediaController : Controller
{
    private readonly IAdminTecnicasMediaCommandService _comandos;
    private readonly ILogger<AdminTecnicasMediaController> _logger;

    public AdminTecnicasMediaController(
        IAdminTecnicasMediaCommandService comandos,
        ILogger<AdminTecnicasMediaController> logger)
    {
        _comandos = comandos ?? throw new ArgumentNullException(nameof(comandos));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Actor administrativo autenticado (claim Name de la cookie).</summary>
    private string Actor => User.Identity?.Name ?? DevAdminCredential.DefaultUser;

    // ── Alta de enlace de video ────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Agregar(
        int id,
        AdminMediaAltaViewModel model,
        string? buscar = null,
        bool? estado = null,
        int? cinturon = null)
    {
        if (!ModelState.IsValid)
            return VolverConError(id, buscar, estado, cinturon, model);

        var resultado = await EjecutarAsync(
            () => _comandos.AgregarAsync(id, new MediaComando(model.Url, model.Titulo, model.Descripcion), Actor),
            "Recurso multimedia agregado correctamente.");

        return await ContinuarAsync(resultado, id, buscar, estado, cinturon, model);
    }

    // ── Edición de un recurso (título, descripción, URL) ───────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        int id,
        int mediaId,
        AdminMediaEdicionViewModel model,
        string? buscar = null,
        bool? estado = null,
        int? cinturon = null)
    {
        model.MediaId = mediaId; // el Id de la ruta es la fuente de verdad (overposting)

        if (!ModelState.IsValid)
            return VolverConError(id, buscar, estado, cinturon, null);

        var resultado = await EjecutarAsync(
            () => _comandos.EditarAsync(
                id, mediaId, new MediaComando(model.Url, model.Titulo, model.Descripcion), Actor),
            "Recurso multimedia actualizado correctamente.");

        return await ContinuarAsync(resultado, id, buscar, estado, cinturon, null);
    }

    // ── Marcar como portada ────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarPortada(
        int id,
        int mediaId,
        string? buscar = null,
        bool? estado = null,
        int? cinturon = null)
    {
        var resultado = await EjecutarAsync(
            () => _comandos.MarcarPortadaAsync(id, mediaId, Actor),
            "Recurso marcado como portada de la técnica.");

        return await ContinuarAsync(resultado, id, buscar, estado, cinturon, null);
    }

    // ── Quitar la asociación (confirma en cliente; el recurso NO se borra) ─

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Quitar(
        int id,
        int mediaId,
        string? buscar = null,
        bool? estado = null,
        int? cinturon = null)
    {
        var resultado = await EjecutarAsync(
            () => _comandos.DesvincularAsync(id, mediaId, Actor),
            "Recurso desvinculado de la técnica. El archivo o enlace compartido se conservó.");

        return await ContinuarAsync(resultado, id, buscar, estado, cinturon, null);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Ejecuta un comando multimedia:
    /// - Rechazo de negocio → TempData de error (los mensajes viajan como
    ///   banner porque la sección tiene formularios propios, no re-render).
    /// - Id desconocido (técnica o recurso) → 404.
    /// - Éxito → TempData de éxito.
    /// - Fallo de infraestructura → log + TempData de error (la transacción
    ///   del servicio revirtió: no hay datos parciales).
    /// </summary>
    private async Task<ResultadoOperacion> EjecutarAsync(
        Func<Task<ResultadoOperacion>> comando,
        string mensajeExito)
    {
        try
        {
            var resultado = await comando();

            if (resultado.Exito && !resultado.Ausente)
                TempData["Exito"] = mensajeExito;

            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo administrativo al ejecutar un comando multimedia de técnicas.");
            return ResultadoOperacion.Fallo(
                "No se pudo completar la operación. Revisá los datos e intentá nuevamente.");
        }
    }

    /// <summary>Desenlace tras ejecutar: 404, error de negocio o redirect a Editar.</summary>
    private async Task<IActionResult> ContinuarAsync(
        ResultadoOperacion resultado,
        int id,
        string? buscar,
        bool? estado,
        int? cinturon,
        AdminMediaAltaViewModel? alta)
    {
        if (resultado.Ausente)
            return NotFound();

        if (!resultado.Exito)
        {
            TempData["Error"] = string.Join(" ", resultado.Errores);
            if (alta is not null)
                ResguardarIntento(alta);
            return VolverAEdicion(id, buscar, estado, cinturon);
        }

        return VolverAEdicion(id, buscar, estado, cinturon);
    }

    /// <summary>Modelo inválido en el alta: banner con los errores + intento resguardado.</summary>
    private IActionResult VolverConError(
        int id,
        string? buscar,
        bool? estado,
        int? cinturon,
        AdminMediaAltaViewModel? alta)
    {
        var errores = ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .ToList();

        TempData["Error"] = errores.Count > 0
            ? string.Join(" ", errores)
            : "Revisá los datos del recurso e intentá nuevamente.";

        if (alta is not null)
            ResguardarIntento(alta);

        return VolverAEdicion(id, buscar, estado, cinturon);
    }

    /// <summary>
    /// Conserva el intento de alta fallido en TempData para repoblar el
    /// formulario (pierde-se el click, no lo escrito).
    /// </summary>
    private void ResguardarIntento(AdminMediaAltaViewModel alta)
    {
        TempData["MediaUrlIntentada"] = alta.Url;
        TempData["MediaTituloIntentado"] = alta.Titulo;
        TempData["MediaDescripcionIntentada"] = alta.Descripcion;
    }

    /// <summary>Redirect a Editar conservando los tres filtros del listado.</summary>
    private RedirectToActionResult VolverAEdicion(int id, string? buscar, bool? estado, int? cinturon)
    {
        var valores = new RouteValueDictionary { ["id"] = id };

        if (!string.IsNullOrWhiteSpace(buscar))
            valores[AdminTecnicasFiltros.ParamBuscar] = buscar.Trim();
        if (estado.HasValue)
            valores[AdminTecnicasFiltros.ParamEstado] = estado.Value;
        if (cinturon.HasValue)
            valores[AdminTecnicasFiltros.ParamCinturon] = cinturon.Value;

        return RedirectToAction(nameof(AdminTecnicasController.Edit), "AdminTecnicas", valores);
    }
}
