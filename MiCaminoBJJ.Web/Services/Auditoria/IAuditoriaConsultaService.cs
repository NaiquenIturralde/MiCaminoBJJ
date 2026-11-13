namespace MiCaminoBJJ.Web.Services.Auditoria;

/// <summary>
/// Consulta de SOLO LECTURA sobre los registros de auditoría (AuditLog).
///
/// Reglas:
/// - Nunca escribe ni modifica datos.
/// - Devuelve únicamente registros REALES de la base: si no hay, la lista
///   viene vacía (no se simulan datos de ejemplo).
/// - El actor se resuelve con el usuario de dominio (UserId) y, cuando no
///   existe, con el "Actor" guardado en Details (cookie de Development).
/// - Nunca se exponen contraseñas, hashes ni tokens: el servicio no lee
///   campos sensibles y no devuelve el contenido completo de Details.
/// </summary>
public interface IAuditoriaConsultaService
{
    /// <summary>
    /// Registros que cumplen el filtro, ordenados del más reciente al más
    /// antiguo. Con <see cref="AuditoriaFiltro.TamanoPagina"/> = 0 devuelve
    /// todos los registros del filtro (modo reporte).
    /// </summary>
    Task<AuditoriaConsulta> ConsultarAsync(
        AuditoriaFiltro filtro,
        CancellationToken cancellationToken = default);

    /// <summary>Tipos de entidad presentes en la auditoría (para el filtro).</summary>
    Task<IReadOnlyList<string>> ObtenerEntidadesAsync(CancellationToken cancellationToken = default);
}
