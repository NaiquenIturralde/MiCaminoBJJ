using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

/// <summary>
/// Pipeline atómico de las operaciones administrativas auditadas (Etapa 1.2).
///
/// Orden garantizado:
/// 1. Autorización (rol Administrador) ANTES de tocar datos o archivos; una
///    denegación deja un único registro PermissionDenied y no ejecuta la
///    operación (el UserId aprobado se le pasa a la operación como actor).
/// 2. Transacción: propia si el llamador no abrió una; si ya existe una
///    transacción activa, la operación y la auditoría se suman a ella.
/// 3. Operación administrativa + auditoría estricta (RecordStrictAsync): si la
///    auditoría falla, la excepción se propaga y la transacción se revierte,
///    de modo que NUNCA quede una modificación administrativa sin traza.
/// 4. Al revertir, el change tracker se limpia para no conservar en memoria la
///    modificación que la base de datos no tiene.
///
/// Solo se usa desde operaciones de escritura administrativa: las lecturas y
/// el flujo de los practicantes no pasan por aquí (no se auditán).
/// </summary>
public static class AdminAuditOperation
{
    public static async Task<TResult> ExecuteAsync<TResult>(
        AppDbContext context,
        IAdminAuthorizationService authorization,
        AuditAction attemptedAction,
        string entityType,
        int entityId,
        Func<int, Task<TResult>> operation,
        Func<int, Task> auditAsync)
    {
        var actorId = await authorization.RequireAdministratorAsync(attemptedAction, entityType, entityId);

        var ownsTransaction = context.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;

        try
        {
            if (ownsTransaction)
                transaction = await context.Database.BeginTransactionAsync();

            var result = await operation(actorId);

            // Auditoría estricta: su fallo aborta (y revierte) toda la operación.
            await auditAsync(actorId);

            if (transaction is not null)
                await transaction.CommitAsync();

            return result;
        }
        catch
        {
            if (ownsTransaction)
                context.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync(); // sin commit => rollback
        }
    }
}

