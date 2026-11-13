namespace MiCaminoBJJ.Web.Services.Auditoria;

/// <summary>
/// Genera el reporte PDF de auditoría.
///
/// El servicio NO consulta la base: recibe las filas ya filtradas y devuelve
/// los bytes del PDF. Así el mismo conjunto de datos que se ve en pantalla es
/// exactamente el que se descarga (registros reales, nunca simulados).
/// </summary>
public interface IAuditoriaPdfService
{
    /// <summary>
    /// Bytes de un PDF con el reporte de auditoría. Cuando <paramref name="filas"/>
    /// viene vacío, el documento incluye un mensaje claro de "sin registros".
    /// </summary>
    Task<byte[]> GenerarAsync(
        IReadOnlyList<AuditoriaFila> filas,
        string usuarioGenerador,
        DateTimeOffset generadoUtc,
        CancellationToken cancellationToken = default);
}
