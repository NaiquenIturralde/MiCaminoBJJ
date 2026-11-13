using System.Text.Json;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;

namespace MiCaminoBJJ.Web.Persistence;

/// <summary>
/// Escritor de auditoría de la WEB (Etapa 3): implementación del CONTRATO
/// COMPARTIDO <see cref="IAuditService"/> sobre <see cref="WebTfDbContext"/>.
///
/// Por qué existe: el <c>AuditService</c> de Infrastructure está atado a
/// <c>AppDbContext</c> (SQLite de MAUI) y modificarlo está fuera de autorización.
/// Este adaptador conserva EXACTAMENTE la misma semántica del servicio original
/// (Etapa 1.1), sin crear un sistema paralelo: misma entidad AuditLog, mismos
/// enums, mismos modos tolerante/estricto y la misma lista blanca de claves:
///
/// - Usa el MISMO WebTfDbContext que el comando administrativo: si el servicio
///   abrió una transacción (BeginTransactionAsync), el registro de auditoría
///   participa de ella → operación y auditoría se confirman o revierten juntas.
/// - RecordAsync: modo tolerante (el fallo queda en el log, no revierte).
/// - RecordStrictAsync: modo estricto (el fallo se propaga y revierte la
///   transacción conjunta) — es el que usan los comandos de técnicas.
/// - AuditLog no se audita a sí mismo.
/// - Lista blanca: se descarta cualquier clave con material sensible
///   (password, hash, salt, token, secret, note).
/// </summary>
public sealed class WebAuditService : IAuditService
{
    private const string AuditLogEntityName = nameof(AuditLog);
    private const int MaxErrorMessageLength = 500;

    private static readonly string[] SensitiveKeyFragments =
        { "password", "hash", "salt", "token", "secret", "note" };

    private readonly WebTfDbContext _context;
    private readonly ILogger<WebAuditService> _logger;

    public WebAuditService(WebTfDbContext context, ILogger<WebAuditService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
    /// Serializa los cambios como JSON descartando claves con material sensible
    /// (misma lista blanca que AuditService de Infrastructure).
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
        value is { Length: > MaxErrorMessageLength } ? value[..MaxErrorMessageLength] : value;
}
