using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Web.Services.Auditoria;

/// <summary>
/// Filtros de la consulta de auditoría (pantalla y reporte PDF).
///
/// Son criterio de LECTURA: no modifican la base ni las reglas de negocio.
/// Un filtro nulo o vacío equivale a "sin filtro".
/// </summary>
public sealed class AuditoriaFiltro
{
    /// <summary>Operación auditada (Create, Update, SoftDelete…); null = todas.</summary>
    public AuditAction? Accion { get; set; }

    /// <summary>Tipo de entidad afectada (nombre CLR); null = todas.</summary>
    public string? Entidad { get; set; }

    /// <summary>Texto libre: nombre o correo del actor, o contenido de Details.</summary>
    public string? Usuario { get; set; }

    /// <summary>Fecha mínima (UTC) de la operación.</summary>
    public DateTime? Desde { get; set; }

    /// <summary>Fecha máxima (UTC) de la operación.</summary>
    public DateTime? Hasta { get; set; }

    /// <summary>Página solicitada (1 = primera).</summary>
    public int Pagina { get; set; } = 1;

    /// <summary>
    /// Filas por página. 0 = sin paginación (modo reporte: se devuelven
    /// todos los registros del filtro, con un tope de seguridad).
    /// </summary>
    public int TamanoPagina { get; set; } = 25;

    /// <summary>True cuando hay al menos un criterio activo.</summary>
    public bool TieneFiltros =>
        Accion.HasValue
        || !string.IsNullOrWhiteSpace(Entidad)
        || !string.IsNullOrWhiteSpace(Usuario)
        || Desde.HasValue
        || Hasta.HasValue;
}

/// <summary>
/// Fila de auditoría lista para pantalla y PDF.
/// No contiene secretos: solo actor, operación, entidad, identificador y resultado.
/// </summary>
public sealed record AuditoriaFila(
    int Id,
    DateTime FechaUtc,
    string Usuario,
    string Operacion,
    string Entidad,
    int EntidadId,
    string Resultado);

/// <summary>Resultado paginado de la consulta de auditoría.</summary>
public sealed class AuditoriaConsulta
{
    public IReadOnlyList<AuditoriaFila> Filas { get; init; } = Array.Empty<AuditoriaFila>();

    /// <summary>Cantidad TOTAL de registros que cumplen el filtro.</summary>
    public int Total { get; init; }

    public int Pagina { get; init; } = 1;

    public int TamanoPagina { get; init; } = 25;

    public int TotalPaginas => TamanoPagina <= 0
        ? 1
        : Math.Max(1, (int)Math.Ceiling(Total / (double)TamanoPagina));

    public bool HayRegistros => Filas.Count > 0;
}

/// <summary>
/// Traducciones de los valores de auditoría a texto legible.
/// Se usan en la pantalla y en el PDF (mismo criterio en ambos).
/// </summary>
public static class AuditoriaEtiquetas
{
    /// <summary>Etiqueta legible de la operación auditada.</summary>
    public static string Operacion(AuditAction action) => action switch
    {
        AuditAction.Create => "Creación",
        AuditAction.Update => "Modificación",
        AuditAction.SoftDelete => "Desactivación",
        AuditAction.Restore => "Reactivación",
        AuditAction.Purge => "Eliminación",
        AuditAction.LoginSuccess => "Inicio de sesión",
        AuditAction.LoginFailed => "Intento de sesión fallido",
        AuditAction.PermissionDenied => "Permiso denegado",
        AuditAction.StatusChange => "Cambio de estado",
        _ => action.ToString()
    };

    /// <summary>Etiqueta legible del resultado de la operación.</summary>
    public static string Resultado(AuditResult result) => result switch
    {
        AuditResult.Success => "Correcto",
        AuditResult.Failure => "Fallido",
        _ => result.ToString()
    };

    /// <summary>Etiqueta legible de la entidad afectada.</summary>
    public static string Entidad(string? entityType) => entityType switch
    {
        "Technique" => "Técnica",
        "TechniqueAlias" => "Alias de técnica",
        "MediaAsset" => "Recurso multimedia",
        "TechniqueMedia" => "Vínculo multimedia",
        "User" => "Usuario",
        "SavedTechnique" => "Técnica guardada",
        "TrainingSession" => "Sesión de entrenamiento",
        "UserProfileMedia" => "Foto de perfil",
        "AuditLog" => "Registro de auditoría",
        null or "" => "(sin entidad)",
        _ => entityType
    };
}
