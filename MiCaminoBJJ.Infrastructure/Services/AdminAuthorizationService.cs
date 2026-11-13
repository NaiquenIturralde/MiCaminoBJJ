using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;

namespace MiCaminoBJJ.Infrastructure.Services;

/// <summary>
/// Guardia de autorización administrativa (Etapa 1.2).
///
/// Verifica identidad y permisos contra ISessionService (misma sesión que usa
/// la UI) y deja traza de cada denegación en AuditLog mediante IAuditService.
/// Nunca escribe datos del usuario (solo el identificador y el motivo), por lo
/// que no puede filtrar contraseñas, tokens ni notas personales.
/// </summary>
public class AdminAuthorizationService : IAdminAuthorizationService
{
    private const string NotAuthenticatedMessage =
        "Sesión requerida: la operación administrativa exige un usuario autenticado.";
    private const string NotAdministratorMessage =
        "Permisos insuficientes: la operación administrativa exige el rol Administrador.";

    private readonly ISessionService _session;
    private readonly IAuditService _audit;

    public AdminAuthorizationService(ISessionService session, IAuditService audit)
    {
        _session = session;
        _audit = audit;
    }

    public async Task<int> RequireAdministratorAsync(AuditAction attemptedAction, string entityType, int entityId)
    {
        var userId = await _session.GetCurrentUserIdAsync();
        if (!userId.HasValue)
        {
            await DenyAsync(null, attemptedAction, entityType, entityId, "NotAuthenticated", NotAuthenticatedMessage);
            throw new UnauthorizedAccessException(NotAuthenticatedMessage);
        }

        var role = await _session.GetUserRoleAsync();
        if (role != UserRole.Administrador)
        {
            await DenyAsync(userId, attemptedAction, entityType, entityId, "InsufficientRole", NotAdministratorMessage);
            throw new UnauthorizedAccessException(NotAdministratorMessage);
        }

        return userId.Value;
    }

    /// <summary>
    /// Único registro PermissionDenied por intento rechazado. Modo tolerante:
    /// una denegación no depende de que la auditoría pueda escribirse.
    /// </summary>
    private Task DenyAsync(
        int? userId,
        AuditAction attemptedAction,
        string entityType,
        int entityId,
        string reason,
        string errorMessage) =>
        _audit.RecordAsync(
            userId,
            AuditAction.PermissionDenied,
            entityType,
            entityId,
            AuditResult.Failure,
            changes: new Dictionary<string, object?>
            {
                ["AttemptedAction"] = attemptedAction.ToString(),
                ["Reason"] = reason,
                ["RequiredRole"] = nameof(UserRole.Administrador)
            },
            errorMessage: errorMessage);
}
