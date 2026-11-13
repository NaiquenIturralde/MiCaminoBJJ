using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Interfaces;

/// <summary>
/// Guardia de autorización para operaciones administrativas (Etapa 1.2).
///
/// Contrato:
/// - Verifica identidad (sesión activa) y permiso (rol Administrador) ANTES de
///   ejecutar cualquier mutación administrativa.
/// - Devuelve el UserId responsable, que debe usarse como actor del AuditLog.
/// - Si la operación no está permitida, escribe un único registro
///   PermissionDenied (vía IAuditService, modo tolerante) y lanza
///   UnauthorizedAccessException SIN ejecutar la operación.
///
/// No reemplaza a IAuditService: lo utiliza. Tampoco audita lecturas.
/// </summary>
public interface IAdminAuthorizationService
{
    /// <summary>
    /// Exige una sesión autenticada con rol Administrador.
    /// </summary>
    /// <param name="attemptedAction">Acción que se intenta ejecutar (para el registro PermissionDenied).</param>
    /// <param name="entityType">Entidad afectada (CLR name). Para altas se usa 0 como EntityId.</param>
    /// <param name="entityId">Identificador del registro afectado; 0 si todavía no existe.</param>
    /// <returns>UserId del administrador responsable.</returns>
    /// <exception cref="UnauthorizedAccessException">Sin sesión o sin rol Administrador.</exception>
    Task<int> RequireAdministratorAsync(AuditAction attemptedAction, string entityType, int entityId);
}
