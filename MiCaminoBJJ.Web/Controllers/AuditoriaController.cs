using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Services.Auditoria;
using MiCaminoBJJ.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MiCaminoBJJ.Web.Controllers;

/// <summary>
/// Pantalla de auditoría del módulo administrativo (cierre TFI).
///
/// Qué hace:
/// - Muestra los registros REALES de <c>AuditLogs</c> (misma tabla que
///   escribe <c>WebAuditService</c> cuando el administrador crea, modifica o
///   desactiva una técnica).
/// - Permite filtrar por operación, entidad, actor y rango de fechas.
/// - Genera un PDF descargable con el MISMO conjunto de datos que se ve en
///   pantalla (nunca datos simulados).
///
/// Seguridad (server-side):
/// - [Authorize(Policy = AdministradorAuditoria)]: exige sesión autenticada
///   con el rol "Administrador" (esquema Identity o cookie administrativa de
///   Development). Un Practicante recibe 403 y nunca ve los registros.
/// - La descarga es un GET (no muta nada) → el antiforgery global no aplica.
/// - Los registros NO incluyen contraseñas, hashes ni tokens: el servicio
///   proyecta solo actor, operación, entidad, id y resultado.
/// </summary>
[Authorize(Policy = AdminPolicies.AdministradorAuditoria)]
public class AuditoriaController : Controller
{
    private const int TamanoPagina = 25;

    private readonly IAuditoriaConsultaService _consulta;
    private readonly IAuditoriaPdfService _pdf;
    private readonly ILogger<AuditoriaController> _logger;

    public AuditoriaController(
        IAuditoriaConsultaService consulta,
        IAuditoriaPdfService pdf,
        ILogger<AuditoriaController> logger)
    {
        _consulta = consulta ?? throw new ArgumentNullException(nameof(consulta));
        _pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Listado de auditoría con filtros.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? accion = null,
        string? entidad = null,
        string? usuario = null,
        DateTime? desde = null,
        DateTime? hasta = null,
        int pagina = 1)
    {
        var filtro = ConstruirFiltro(accion, entidad, usuario, desde, hasta, pagina, TamanoPagina);
        var consulta = await _consulta.ConsultarAsync(filtro);
        var entidades = await _consulta.ObtenerEntidadesAsync();

        var model = new AuditoriaIndexViewModel
        {
            Registros = consulta.Filas.Select(AuditoriaFilaViewModel.Desde).ToList(),
            TotalRegistros = consulta.Total,
            Pagina = consulta.Pagina,
            TotalPaginas = consulta.TotalPaginas,
            AccionSeleccionada = accion,
            EntidadSeleccionada = entidad,
            Usuario = usuario,
            Desde = desde,
            Hasta = hasta,
            Acciones = AccionesDisponibles(),
            Entidades = entidades
                .Select(e => new AuditoriaOpcionViewModel
                {
                    Valor = e,
                    Texto = AuditoriaEtiquetas.Entidad(e)
                })
                .ToList()
        };

        return View(model);
    }

    /// <summary>
    /// Descarga el reporte PDF de auditoría con los filtros aplicados.
    /// Devuelve el archivo como <c>application/pdf</c> (adjunto).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DescargarPdf(
        string? accion = null,
        string? entidad = null,
        string? usuario = null,
        DateTime? desde = null,
        DateTime? hasta = null)
    {
        try
        {
            // Tamaño 0 = sin paginación: el reporte incluye TODOS los registros
            // del filtro (no solo la página visible).
            var filtro = ConstruirFiltro(accion, entidad, usuario, desde, hasta, 1, 0);
            var consulta = await _consulta.ConsultarAsync(filtro);

            var actor = User.Identity?.Name ?? "Administrador";
            var bytes = await _pdf.GenerarAsync(consulta.Filas, actor, DateTimeOffset.Now);

            var nombreArchivo = $"MiCaminoBJJ_Reporte_Auditoria_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            _logger.LogInformation(
                "Reporte PDF de auditoría generado por {Actor}: {Cantidad} registros.",
                actor, consulta.Filas.Count);

            return File(bytes, "application/pdf", nombreArchivo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo al generar el reporte PDF de auditoría.");
            return Problem(
                "No se pudo generar el reporte PDF de auditoría. Intentá nuevamente.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>Construye el filtro de consulta a partir de los parámetros GET.</summary>
    private static AuditoriaFiltro ConstruirFiltro(
        string? accion,
        string? entidad,
        string? usuario,
        DateTime? desde,
        DateTime? hasta,
        int pagina,
        int tamanoPagina)
    {
        AuditAction? accionAuditada = null;
        if (!string.IsNullOrWhiteSpace(accion)
            && Enum.TryParse<AuditAction>(accion.Trim(), ignoreCase: true, out var valor))
        {
            accionAuditada = valor;
        }

        return new AuditoriaFiltro
        {
            Accion = accionAuditada,
            Entidad = string.IsNullOrWhiteSpace(entidad) ? null : entidad.Trim(),
            Usuario = string.IsNullOrWhiteSpace(usuario) ? null : usuario.Trim(),
            Desde = desde,
            Hasta = hasta,
            Pagina = pagina < 1 ? 1 : pagina,
            TamanoPagina = tamanoPagina
        };
    }

    /// <summary>Operaciones auditadas disponibles en el filtro.</summary>
    private static List<AuditoriaOpcionViewModel> AccionesDisponibles() =>
        Enum.GetValues<AuditAction>()
            .Select(a => new AuditoriaOpcionViewModel
            {
                Valor = a.ToString(),
                Texto = AuditoriaEtiquetas.Operacion(a)
            })
            .ToList();
}
