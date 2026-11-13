using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Infrastructure.Services;
using MiCaminoBJJ.Tests.Infrastructure;

namespace MiCaminoBJJ.Tests.Services;

/// <summary>
/// Etapa 1.1 — Infraestructura de auditoría.
/// Cubre: persistencia completa del registro, lista blanca de datos sensibles,
/// anti-recursión (AuditLog no se audita a sí mismo), modo tolerante vs.
/// estricto y consistencia transaccional conjunta con la operación principal.
/// </summary>
public class AuditServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db;

    public AuditServiceTests() => _db = new SqliteTestDatabase();

    public void Dispose() => _db.Dispose();

    private AuditService CreateSut() =>
        new(_db.Context, NullLogger<AuditService>.Instance);

    // ── Persistencia ─────────────────────────────────────────────────────

    [Fact]
    public async Task RecordAsync_PersistsAllFields()
    {
        var sut = CreateSut();

        await sut.RecordAsync(
            userId: 42,
            action: AuditAction.Update,
            entityType: "TrainingSession",
            entityId: 7,
            result: AuditResult.Success,
            changes: new Dictionary<string, object?> { ["DurationMinutes"] = 90 },
            errorMessage: null);

        var entry = await _db.CreateContext().AuditLogs.SingleAsync();
        entry.UserId.Should().Be(42);
        entry.Action.Should().Be(AuditAction.Update);
        entry.EntityType.Should().Be("TrainingSession");
        entry.EntityId.Should().Be(7);
        entry.Result.Should().Be(AuditResult.Success);
        entry.Details.Should().Contain("DurationMinutes").And.Contain("90");
        entry.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task RecordAsync_SystemActor_StoresNullUserId()
    {
        var sut = CreateSut();

        await sut.RecordAsync(null, AuditAction.Create, "Technique", 1);

        var entry = await _db.CreateContext().AuditLogs.SingleAsync();
        entry.UserId.Should().BeNull();
    }

    [Fact]
    public async Task RecordAsync_WithoutChanges_StoresNullDetails()
    {
        var sut = CreateSut();

        await sut.RecordAsync(1, AuditAction.Purge, "MediaAsset", 3);

        var entry = await _db.CreateContext().AuditLogs.SingleAsync();
        entry.Details.Should().BeNull();
    }

    // ── Seguridad: lista blanca de datos sensibles ───────────────────────

    [Theory]
    [InlineData("PasswordHash")]
    [InlineData("PasswordSalt")]
    [InlineData("password")]
    [InlineData("Token")]
    [InlineData("RefreshSecret")]
    [InlineData("Notes")]
    [InlineData("PersonalNotes")]
    public async Task RecordAsync_SensitiveKeys_AreExcludedFromDetails(string sensitiveKey)
    {
        var sut = CreateSut();
        var changes = new Dictionary<string, object?>
        {
            [sensitiveKey] = "SENSITIVE-VALUE-DO-NOT-PERSIST",
            ["Name"] = "Escape de montada"
        };

        await sut.RecordAsync(1, AuditAction.Update, "User", 5, changes: changes);

        var entry = await _db.CreateContext().AuditLogs.SingleAsync();
        entry.Details.Should().NotContain("SENSITIVE-VALUE-DO-NOT-PERSIST");
        entry.Details.Should().NotContain(sensitiveKey);
        entry.Details.Should().Contain("Escape de montada");
    }

    [Fact]
    public async Task RecordAsync_AllKeysSensitive_StoresNullDetails()
    {
        var sut = CreateSut();
        var changes = new Dictionary<string, object?> { ["PasswordHash"] = "abc" };

        await sut.RecordAsync(1, AuditAction.Update, "User", 5, changes: changes);

        var entry = await _db.CreateContext().AuditLogs.SingleAsync();
        entry.Details.Should().BeNull();
    }

    [Fact]
    public async Task RecordAsync_LongErrorMessage_IsTruncatedTo500()
    {
        var sut = CreateSut();
        var longMessage = new string('x', 700);

        await sut.RecordAsync(1, AuditAction.Purge, "Technique", 9,
            result: AuditResult.Failure, errorMessage: longMessage);

        var entry = await _db.CreateContext().AuditLogs.SingleAsync();
        entry.ErrorMessage.Should().HaveLength(500);
    }

    // ── Anti-recursión ───────────────────────────────────────────────────

    [Fact]
    public async Task RecordAsync_AttemptToAuditAuditLog_SkipsWithoutPersisting()
    {
        var sut = CreateSut();

        await sut.RecordAsync(1, AuditAction.Update, "AuditLog", 1);

        var entries = await _db.CreateContext().AuditLogs.ToListAsync();
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordStrictAsync_AttemptToAuditAuditLog_Throws()
    {
        var sut = CreateSut();

        var act = async () => await sut.RecordStrictAsync(1, AuditAction.Update, "AuditLog", 1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot audit itself*");
        (await _db.CreateContext().AuditLogs.AnyAsync()).Should().BeFalse();
    }

    // ── Modo tolerante vs. estricto ──────────────────────────────────────

    [Fact]
    public async Task RecordStrictAsync_PersistsEntry()
    {
        var sut = CreateSut();

        await sut.RecordStrictAsync(2, AuditAction.SoftDelete, "TrainingSession", 11,
            result: AuditResult.Success,
            changes: new Dictionary<string, object?> { ["IsDeleted"] = true });

        var entry = await _db.CreateContext().AuditLogs.SingleAsync();
        entry.Action.Should().Be(AuditAction.SoftDelete);
        entry.Result.Should().Be(AuditResult.Success);
    }

    [Fact]
    public async Task RecordStrictAsync_DatabaseFailure_PropagatesException()
    {
        var sut = CreateSut();
        // Romper el esquema simula un fallo de persistencia real (tabla ausente).
        _db.Execute("DROP TABLE AuditLogs;");

        var act = async () => await sut.RecordStrictAsync(1, AuditAction.Purge, "Technique", 1);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task RecordAsync_DatabaseFailure_DoesNotThrow()
    {
        var sut = CreateSut();
        _db.Execute("DROP TABLE AuditLogs;");

        var act = async () => await sut.RecordAsync(1, AuditAction.Purge, "Technique", 1);

        await act.Should().NotThrowAsync();
    }

    // ── Consistencia transaccional ───────────────────────────────────────

    [Fact]
    public async Task Audit_InAmbientTransaction_RollsBackTogetherWithOperation()
    {
        var sut = CreateSut();

        await using (var transaction = await _db.Context.Database.BeginTransactionAsync())
        {
            // Operación administrativa ficticia (registro directo en el mismo
            // contexto) + auditoría: ambas pasan por el mismo SaveChanges y la
            // misma transacción.
            _db.Context.Add(new MiCaminoBJJ.Core.Entities.AuditLog
            {
                UserId = 1,
                Action = AuditAction.Purge,
                EntityType = "Technique",
                EntityId = 100
            });
            await sut.RecordStrictAsync(1, AuditAction.Purge, "Technique", 100);
            await transaction.RollbackAsync();
        }

        var entries = await _db.CreateContext().AuditLogs.ToListAsync();
        entries.Should().BeEmpty("la auditoría participó de la transacción y revirtió junto con la operación");
    }

    [Fact]
    public async Task Audit_InAmbientTransaction_CommitsTogetherWithOperation()
    {
        var sut = CreateSut();

        await using (var transaction = await _db.Context.Database.BeginTransactionAsync())
        {
            await sut.RecordStrictAsync(1, AuditAction.StatusChange, "User", 3,
                changes: new Dictionary<string, object?> { ["Status"] = "Suspendido" });
            await transaction.CommitAsync();
        }

        var entry = await _db.CreateContext().AuditLogs.SingleAsync();
        entry.Action.Should().Be(AuditAction.StatusChange);
        entry.Details.Should().Contain("Suspendido");
    }
}
