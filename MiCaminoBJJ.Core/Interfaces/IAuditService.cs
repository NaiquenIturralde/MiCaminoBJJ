using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Interfaces;

/// <summary>
/// Escribe registros de auditoría (AuditLog).
///
/// Reglas de uso (Etapa 1.1):
/// - Toda escritura se hace con el MISMO AppDbContext del servicio que ejecuta
///   la operación administrativa. Si ese contexto tiene una transacción abierta
///   (BeginTransactionAsync), el registro de auditoría participa de ella:
///   operación y auditoría se confirman o se revierten juntas.
/// - RecordAsync es el modo normal: un fallo de auditoría se registra en log
///   pero NO revierte la operación principal.
/// - RecordStrictAsync es el modo para operaciones sensibles (purgas, cambios
///   de estado): si la escritura de auditoría falla, la excepción se propaga
///   y revierte la transacción completa.
/// - AuditLog no se audita a sí mismo: registrar la entidad "AuditLog" se
///   rechaza (RecordAsync lo descarta con warning; RecordStrictAsync lanza).
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Registra un evento de auditoría. Modo tolerante: nunca lanza excepción
    /// hacia el llamador; un fallo se escribe en el log de la aplicación.
    /// Participa de la transacción activa del AppDbContext si la hay.
    /// </summary>
    Task RecordAsync(
        int? userId,
        AuditAction action,
        string entityType,
        int entityId,
        AuditResult result = AuditResult.Success,
        IReadOnlyDictionary<string, object?>? changes = null,
        string? errorMessage = null);

    /// <summary>
    /// Registra un evento de auditoría en modo estricto: las fallas se propagan
    /// para revertir la transacción conjunta con la operación administrativa.
    /// </summary>
    Task RecordStrictAsync(
        int? userId,
        AuditAction action,
        string entityType,
        int entityId,
        AuditResult result = AuditResult.Success,
        IReadOnlyDictionary<string, object?>? changes = null,
        string? errorMessage = null);
}
