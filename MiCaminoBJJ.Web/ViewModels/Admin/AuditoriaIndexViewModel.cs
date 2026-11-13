using System.Globalization;
using MiCaminoBJJ.Web.Services.Auditoria;

namespace MiCaminoBJJ.Web.ViewModels.Admin;

/// <summary>
/// Listado de auditoría del administrador (pantalla + reporte PDF).
/// Solo presenta datos REALES leídos de la tabla AuditLogs.
/// </summary>
public sealed class AuditoriaIndexViewModel
{
    public IReadOnlyList<AuditoriaFilaViewModel> Registros { get; set; } =
        Array.Empty<AuditoriaFilaViewModel>();

    /// <summary>Operaciones auditadas disponibles para el filtro.</summary>
    public IReadOnlyList<AuditoriaOpcionViewModel> Acciones { get; set; } =
        Array.Empty<AuditoriaOpcionViewModel>();

    /// <summary>Entidades presentes en la auditoría (para el filtro).</summary>
    public IReadOnlyList<AuditoriaOpcionViewModel> Entidades { get; set; } =
        Array.Empty<AuditoriaOpcionViewModel>();

    /// <summary>Operación seleccionada (nombre del enum) o null.</summary>
    public string? AccionSeleccionada { get; set; }

    /// <summary>Entidad seleccionada (nombre CLR) o null.</summary>
    public string? EntidadSeleccionada { get; set; }

    /// <summary>Texto libre de búsqueda del actor.</summary>
    public string? Usuario { get; set; }

    /// <summary>Fecha mínima (inclusive).</summary>
    public DateTime? Desde { get; set; }

    /// <summary>Fecha máxima (inclusive).</summary>
    public DateTime? Hasta { get; set; }

    public int Pagina { get; set; } = 1;

    public int TotalPaginas { get; set; } = 1;

    /// <summary>Cantidad total de registros que cumplen el filtro.</summary>
    public int TotalRegistros { get; set; }

    public bool HayRegistros => Registros.Count > 0;

    public bool HayPaginaAnterior => Pagina > 1;

    public bool HayPaginaSiguiente => Pagina < TotalPaginas;

    public bool TieneFiltros =>
        !string.IsNullOrWhiteSpace(AccionSeleccionada)
        || !string.IsNullOrWhiteSpace(EntidadSeleccionada)
        || !string.IsNullOrWhiteSpace(Usuario)
        || Desde.HasValue
        || Hasta.HasValue;

    /// <summary>Query string con los filtros actuales (sin página).</summary>
    public string QueryStringFiltros()
    {
        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(AccionSeleccionada))
            partes.Add($"accion={Uri.EscapeDataString(AccionSeleccionada)}");
        if (!string.IsNullOrWhiteSpace(EntidadSeleccionada))
            partes.Add($"entidad={Uri.EscapeDataString(EntidadSeleccionada)}");
        if (!string.IsNullOrWhiteSpace(Usuario))
            partes.Add($"usuario={Uri.EscapeDataString(Usuario)}");
        if (Desde.HasValue)
            partes.Add($"desde={Desde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        if (Hasta.HasValue)
            partes.Add($"hasta={Hasta.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");

        return partes.Count == 0 ? string.Empty : "?" + string.Join("&", partes);
    }

    /// <summary>Query string de los filtros con la página indicada (paginación).</summary>
    public string QueryStringPagina(int pagina) =>
        $"{QueryStringFiltros()}&pagina={pagina.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>URL de descarga del PDF con los filtros actuales.</summary>
    public string UrlDescargarPdf => $"/Auditoria/DescargarPdf{QueryStringFiltros()}";
}

/// <summary>Fila de auditoría lista para mostrar.</summary>
public sealed class AuditoriaFilaViewModel
{
    public int Id { get; set; }

    /// <summary>Fecha y hora de la operación (hora local del servidor).</summary>
    public string Fecha { get; set; } = string.Empty;

    /// <summary>Actor responsable de la operación.</summary>
    public string Usuario { get; set; } = string.Empty;

    /// <summary>Operación (creación, modificación, desactivación…).</summary>
    public string Operacion { get; set; } = string.Empty;

    /// <summary>Entidad afectada.</summary>
    public string Entidad { get; set; } = string.Empty;

    /// <summary>Identificador del registro afectado.</summary>
    public int EntidadId { get; set; }

    /// <summary>Resultado de la operación ("Correcto" / "Fallido").</summary>
    public string Resultado { get; set; } = string.Empty;

    public bool EsExitoso => string.Equals(Resultado, "Correcto", StringComparison.Ordinal);

    /// <summary>Proyecta una fila de la consulta al modelo de la vista.</summary>
    public static AuditoriaFilaViewModel Desde(AuditoriaFila fila) => new()
    {
        Id = fila.Id,
        Fecha = fila.FechaUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
        Usuario = fila.Usuario,
        Operacion = fila.Operacion,
        Entidad = fila.Entidad,
        EntidadId = fila.EntidadId,
        Resultado = fila.Resultado
    };
}

/// <summary>Opción de un desplegable de filtro (valor + texto visible).</summary>
public sealed class AuditoriaOpcionViewModel
{
    public string Valor { get; set; } = string.Empty;

    public string Texto { get; set; } = string.Empty;
}
