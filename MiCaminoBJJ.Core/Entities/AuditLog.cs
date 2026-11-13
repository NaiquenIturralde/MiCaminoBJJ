using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Entities;

/// <summary>
/// Registro inmutable de auditoría. Escribido únicamente por IAuditService.
///
/// Diseño (Etapa 1.1):
/// - UserId es nullable y SIN clave foránea: la trazabilidad debe sobrevivir
///   a la purga futura del usuario (el registro queda huérfano a propósito).
/// - Details guarda solo propiedades de lista blanca en JSON (nunca
///   PasswordHash, PasswordSalt, tokens ni notas personales).
/// - AuditLog NUNCA se audita a sí mismo (regla aplicada en AuditService).
/// </summary>
public class AuditLog
{
    public int Id { get; set; }

    /// <summary>Actor. NULL = sistema (seed, migración, inicialización).</summary>
    public int? UserId { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>Nombre CLR de la entidad afectada, p. ej. "TrainingSession".</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Identificador del registro afectado dentro de su tabla.</summary>
    public int EntityId { get; set; }

    /// <summary>Instante UTC de la operación.</summary>
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public AuditResult Result { get; set; } = AuditResult.Success;

    /// <summary>
    /// Cambios relevantes serializados en JSON (solo propiedades de lista blanca).
    /// NULL cuando no hay cambios de estado que reportar.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>Mensaje breve de error cuando Result == Failure.</summary>
    public string? ErrorMessage { get; set; }
}
