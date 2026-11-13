using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Tests.Infrastructure;

/// <summary>
/// Etapa 1.1 — Migración aditiva AddAuditLog sobre una COPIA TEMPORAL poblada
/// (archivo en %TEMP%, eliminado al terminar). La base real de la app no se toca.
///
/// Frontera verificada: base con InitialCreate + AddTechniqueAliasesAndNameNormalized
/// + datos reales → aplicar AddAuditLog → conservación total + tabla AuditLogs usable.
/// </summary>
public class AuditLogMigrationTests : IDisposable
{
    private const string PreAuditMigration = "20261023200525_AddTechniqueAliasesAndNameNormalized";

    private readonly string _dbPath;

    public AuditLogMigrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"micaminobjj_audit_migration_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Estado PRE-etapa-1.1: esquema y datos anteriores a AuditLog.</summary>
    private void CreatePopulatedPreAuditDatabase()
    {
        using var context = CreateContext();

        context.GetService<IMigrator>().Migrate(PreAuditMigration);

        using var cmd = context.Database.GetDbConnection().CreateCommand();
        context.Database.OpenConnection();
        cmd.CommandText = """
            INSERT INTO Belts (Name, DisplayOrder, CreatedAt) VALUES ('Blanco', 1, '2026-01-01 00:00:00');
            INSERT INTO TechniqueCategories (Name, DisplayOrder, CreatedAt) VALUES ('Sumisión', 0, '2026-01-01 00:00:00');
            INSERT INTO Positions (Name, DisplayOrder, CreatedAt) VALUES ('Espalda', 0, '2026-01-01 00:00:00');

            INSERT INTO Techniques (Name, NameNormalized, Description, Objective, BeltId, CategoryId, PositionId, IsActive, CreatedAt)
            VALUES ('Mata león', 'MATA LEON', 'd', 'o', 1, 1, 1, 1, '2026-01-01 00:00:00');

            INSERT INTO Users (Name, Email, PasswordHash, PasswordSalt, BeltRank, Role, CreatedAt)
            VALUES ('Tester', 't@t.com', 'h', 's', 'Blanco', 0, '2026-01-01 00:00:00');

            INSERT INTO TrainingSessions (UserId, TrainingType, Date, DurationMinutes, Feeling, Notes, CreatedAt)
            VALUES (1, 1, '2026-05-10 18:00:00', 60, 0, 'entrenamiento previo', '2026-05-10 18:00:00');

            INSERT INTO TechniquePractices (UserId, TechniqueId, TrainingSessionId, PracticedAt, Repetitions, DurationMinutes, ConfidenceLevel, Notes, CreatedAt)
            VALUES (1, 1, 1, '2026-05-10 18:00:00', 20, 10, 2, 'ok', '2026-05-10 18:00:00');
            """;
        cmd.ExecuteNonQuery();
        context.Database.CloseConnection();
    }

    [Fact]
    public async Task Migration_AddAuditLog_OnPopulatedDatabase_PreservesAllExistingData()
    {
        CreatePopulatedPreAuditDatabase();

        using var context = CreateContext();

        // Aplicar SOLO la nueva migración aditiva (ya aplicadas las dos previas).
        await context.Database.MigrateAsync();

        // Datos preexistentes intactos.
        (await context.Techniques.CountAsync()).Should().Be(1);
        (await context.Users.CountAsync()).Should().Be(1);
        (await context.TrainingSessions.CountAsync()).Should().Be(1);
        (await context.TechniquePractices.CountAsync()).Should().Be(1);

        var session = await context.TrainingSessions.SingleAsync();
        session.DurationMinutes.Should().Be(60);
        session.Notes.Should().Be("entrenamiento previo");

        var practice = await context.TechniquePractices.SingleAsync();
        practice.Repetitions.Should().Be(20);
        practice.ConfidenceLevel.Should().Be(ConfidenceLevel.Alta);

        // La tabla nueva existe y es usable.
        (await context.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Migration_AddAuditLog_IsIdempotent_AndBackfillUnaffected()
    {
        CreatePopulatedPreAuditDatabase();

        using var context = CreateContext();
        await context.Database.MigrateAsync();

        // Migrar dos veces no debe fallar ni duplicar.
        await context.Database.MigrateAsync();

        // Escribir un registro tras la migración funciona (esquema correcto).
        context.AuditLogs.Add(new Core.Entities.AuditLog
        {
            UserId = 1,
            Action = AuditAction.Create,
            EntityType = "Technique",
            EntityId = 1,
            Result = AuditResult.Success
        });
        await context.SaveChangesAsync();

        var entry = await context.AuditLogs.SingleAsync();
        entry.UserId.Should().Be(1);
        entry.EntityType.Should().Be("Technique");
        entry.TimestampUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        // El backfill idempotente posterior (flujo de DatabaseInitializer) sigue correcto.
        var backfill = new MiCaminoBJJ.Infrastructure.Services.DataNormalizationService(
            context, Microsoft.Extensions.Logging.Abstractions.NullLogger<MiCaminoBJJ.Infrastructure.Services.DataNormalizationService>.Instance);
        (await backfill.BackfillTechniqueNamesAsync()).Should().Be(0, "la técnica ya tenía NameNormalized");
    }

    [Fact]
    public async Task Migration_AddAuditLog_KeepsExistingMigrationsUnchanged()
    {
        CreatePopulatedPreAuditDatabase();

        using var context = CreateContext();
        await context.Database.MigrateAsync();

        // Las migraciones previas siguen registradas en __EFMigrationsHistory
        // junto con la nueva: la historia completa es la esperada.
        var applied = await context.Database.GetAppliedMigrationsAsync();
        applied.Should().Contain(new[]
        {
            "20260728201123_InitialCreate",
            "20261023200525_AddTechniqueAliasesAndNameNormalized",
            "20261029023742_AddAuditLog"
        });
    }
}
