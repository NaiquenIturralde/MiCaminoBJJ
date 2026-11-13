using System.Text.Json;
using Microsoft.Extensions.Logging;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

/// <summary>
/// Escritor de auditoría (Etapa 1.1).
///
/// Consistencia transaccional: usa el MISMO AppDbContext que inyecta el
/// contenedor por scope. Si el servicio administrativo abrió una transacción
/// con ese contexto (BeginTransactionAsync), SaveChanges del audit queda
/// dentro de ella: operación y auditoría se confirman o revierten juntas.
///
/// Seguridad:
/// - Lista blanca de claves: se serializan solo los cambios entregados por el
///   llamador, filtrando cualquier clave que contenga material sensible
///   (password, hash, salt, token, secret, note).
/// - Anti-recursión: nunca audita a "AuditLog".
/// </summary>
public class AuditService : IAuditService
{
    private const string AuditLogEntityName = nameof(AuditLog);
    private const int MaxErrorMessageLength = 500;

    private static readonly string[] SensitiveKeyFragments =
        { "password", "hash", "salt", "token", "secret", "note" };

    private readonly AppDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AppDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task RecordAsync(
        int? userId,
        AuditAction action,
        string entityType,
        int entityId,
        AuditResult result = AuditResult.Success,
        IReadOnlyDictionary<string, object?>? changes = null,
        string? errorMessage = null)
    {
        if (!CanAudit(entityType))
        {
            _logger.LogWarning(
                "Audit self-reference blocked: attempted to audit '{EntityType}'#{EntityId}. Record skipped.",
                entityType, entityId);
            return;
        }

        try
        {
            await WriteAsync(userId, action, entityType, entityId, result, changes, errorMessage);
        }
        catch (Exception ex)
        {
            // Modo tolerante: la auditoría fallida no revierte la operación
            // principal; el fallo queda en el log de la aplicación.
            _logger.LogError(ex,
                "Failed to write audit entry for {EntityType}#{EntityId} ({Action}).",
                entityType, entityId, action);
        }
    }

    public async Task RecordStrictAsync(
        int? userId,
        AuditAction action,
        string entityType,
        int entityId,
        AuditResult result = AuditResult.Success,
        IReadOnlyDictionary<string, object?>? changes = null,
        string? errorMessage = null)
    {
        if (!CanAudit(entityType))
            throw new InvalidOperationException(
                $"AuditLog cannot audit itself (attempted entity: '{entityType}').");

        // Sin try/catch: cualquier fallo se propaga y revierte la transacción
        // conjunta con la operación administrativa sensible.
        await WriteAsync(userId, action, entityType, entityId, result, changes, errorMessage);
    }

    private static bool CanAudit(string entityType) =>
        !string.Equals(entityType, AuditLogEntityName, StringComparison.Ordinal);

    private async Task WriteAsync(
        int? userId,
        AuditAction action,
        string entityType,
        int entityId,
        AuditResult result,
        IReadOnlyDictionary<string, object?>? changes,
        string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("EntityType is required.", nameof(entityType));

        var entry = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            TimestampUtc = DateTime.UtcNow,
            Result = result,
            Details = SerializeChanges(changes),
            ErrorMessage = Truncate(errorMessage)
        };

        _context.AuditLogs.Add(entry);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Serializa los cambios como JSON descartando claves con material sensible.
    /// Devuelve null si no hay cambios o si todos fueron filtrados.
    /// </summary>
    internal static string? SerializeChanges(IReadOnlyDictionary<string, object?>? changes)
    {
        if (changes is null || changes.Count == 0)
            return null;

        var safe = new Dictionary<string, object?>(changes.Count, StringComparer.Ordinal);
        foreach (var (key, value) in changes)
        {
            if (ContainsSensitiveFragment(key))
                continue;

            safe[key] = value;
        }

        if (safe.Count == 0)
            return null;

        return JsonSerializer.Serialize(safe);
    }

    private static bool ContainsSensitiveFragment(string key) =>
        SensitiveKeyFragments.Any(fragment =>
            key.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string? Truncate(string? value) =>
        value is { Length: > MaxErrorMessageLength }
            ? value[..MaxErrorMessageLength]
            : value;
}
