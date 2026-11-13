using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Infrastructure.Data;
using MiCaminoBJJ.Infrastructure.Services;

namespace MiCaminoBJJ.Tests.Infrastructure;

/// <summary>
/// Migración aditiva 2.2 sobre una COPIA TEMPORAL de base poblada
/// (archivo en %TEMP%, eliminado al terminar). La base real de la app no se toca.
///
/// Escenario: base con esquema InitialCreate + datos reales (técnicas, entrenamientos,
/// prácticas) → aplicar migración AddTechniqueAliasesAndNameNormalized →
/// verificar conservación total → backfill idempotente con TextNormalizer.
/// </summary>
public class MigrationSafetyTests : IDisposable
{
    private readonly string _dbPath;

    public MigrationSafetyTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"micaminobjj_migration_test_{Guid.NewGuid():N}.db");
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

    /// <summary>Replica el estado PRE-etapa-2.2: solo InitialCreate + datos poblados.</summary>
    private void CreatePopulatedLegacyDatabase()
    {
        using var context = CreateContext();

        // 1) Aplicar ÚNICAMENTE la migración inicial (esquema antiguo, sin NameNormalized).
        context.GetService<IMigrator>().Migrate("20260728201123_InitialCreate");

        // 2) Poblar con datos como los dejaba la app antes de esta etapa.
        using (var cmd = context.Database.GetDbConnection().CreateCommand())
        {
            context.Database.OpenConnection();
            cmd.CommandText = """
                INSERT INTO Belts (Name, DisplayOrder, CreatedAt) VALUES ('Blanco', 1, '2026-01-01 00:00:00');
                INSERT INTO TechniqueCategories (Name, DisplayOrder, CreatedAt) VALUES ('Sumisión', 0, '2026-01-01 00:00:00');
                INSERT INTO Positions (Name, DisplayOrder, CreatedAt) VALUES ('Espalda', 0, '2026-01-01 00:00:00');

                INSERT INTO Techniques (Name, Description, Objective, BeltId, CategoryId, PositionId, IsActive, CreatedAt)
                VALUES ('Mata león', 'd', 'o', 1, 1, 1, 1, '2026-01-01 00:00:00');
                INSERT INTO Techniques (Name, Description, Objective, BeltId, CategoryId, PositionId, IsActive, CreatedAt)
                VALUES ('América', 'd', 'o', 1, 1, 1, 1, '2026-01-01 00:00:00');
                INSERT INTO Techniques (Name, Description, Objective, BeltId, CategoryId, PositionId, IsActive, CreatedAt)
                VALUES ('Óscar del Ñandú', 'd', 'o', 1, 1, 1, 1, '2026-01-01 00:00:00');

                INSERT INTO Users (Name, Email, PasswordHash, PasswordSalt, BeltRank, Role, CreatedAt)
                VALUES ('Tester', 't@t.com', 'h', 's', 'Blanco', 1, '2026-01-01 00:00:00');

                INSERT INTO TrainingSessions (UserId, TrainingType, Date, DurationMinutes, Feeling, Notes, CreatedAt)
                VALUES (1, 1, '2026-05-10 18:00:00', 60, 0, 'entrenamiento previo', '2026-05-10 18:00:00');

                INSERT INTO TechniquePractices (UserId, TechniqueId, TrainingSessionId, PracticedAt, Repetitions, DurationMinutes, ConfidenceLevel, Notes, CreatedAt)
                VALUES (1, 1, 1, '2026-05-10 18:00:00', 20, 10, 2, 'ok', '2026-05-10 18:00:00');
                """;
            cmd.ExecuteNonQuery();
            context.Database.CloseConnection();
        }
    }

    [Fact]
    public async Task Migration_OnPopulatedDatabase_PreservesAllExistingData()
    {
        CreatePopulatedLegacyDatabase();

        using var context = CreateContext();

        // 3) Aplicar TODAS las migraciones pendientes (la nueva, aditiva).
        await context.Database.MigrateAsync();

        // 4) Los datos preexistentes quedan intactos.
        (await context.Techniques.CountAsync()).Should().Be(3);
        (await context.TrainingSessions.CountAsync()).Should().Be(1);
        (await context.TechniquePractices.CountAsync()).Should().Be(1);

        var session = await context.TrainingSessions.SingleAsync();
        session.DurationMinutes.Should().Be(60);
        session.Notes.Should().Be("entrenamiento previo");

        var practice = await context.TechniquePractices.SingleAsync();
        practice.Repetitions.Should().Be(20);
        practice.ConfidenceLevel.Should().Be(MiCaminoBJJ.Core.Enums.ConfidenceLevel.Alta);

        // 5) Columna nueva presente y NULL en filas preexistentes (antes del backfill).
        var legacy = await context.Techniques.SingleAsync(t => t.Name == "América");
        legacy.NameNormalized.Should().BeNull();

        // 6) Tabla de alias creada y consultable.
        context.TechniqueAliases.Should().NotBeNull();
        (await context.TechniqueAliases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Migration_ThenBackfill_NormalizesLegacyRows_WithTextNormalizerRule()
    {
        CreatePopulatedLegacyDatabase();

        using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();

            var backfill = new DataNormalizationService(context, NullLogger<DataNormalizationService>.Instance);

            // Idempotente sobre base poblada real.
            (await backfill.BackfillTechniqueNamesAsync()).Should().Be(3);
            (await backfill.BackfillTechniqueNamesAsync()).Should().Be(0);

            var techniques = await context.Techniques.OrderBy(t => t.Id).ToListAsync();
            techniques.Should().HaveCount(3);

            // EXACTAMENTE la regla de TextNormalizer (no UPPER() de SQLite):
            techniques.Single(t => t.Name == "Mata león").NameNormalized.Should().Be("MATA LEON");
            techniques.Single(t => t.Name == "América").NameNormalized.Should().Be("AMERICA");
            techniques.Single(t => t.Name == "Óscar del Ñandú").NameNormalized.Should().Be("OSCAR DEL NANDU");

            foreach (var t in techniques)
                t.NameNormalized.Should().Be(TextNormalizer.Normalize(t.Name));
        }
    }

    [Fact]
    public async Task Migration_BackfillDoesNotTouchTrainingData()
    {
        CreatePopulatedLegacyDatabase();

        using var context = CreateContext();
        await context.Database.MigrateAsync();

        var before = await context.TrainingSessions.SingleAsync();
        var practiceBefore = await context.TechniquePractices.SingleAsync();

        var backfill = new DataNormalizationService(context, NullLogger<DataNormalizationService>.Instance);
        await backfill.BackfillTechniqueNamesAsync();

        var after = await context.TrainingSessions.SingleAsync();
        var practiceAfter = await context.TechniquePractices.SingleAsync();

        after.Id.Should().Be(before.Id);
        after.Date.Should().Be(before.Date);
        after.Notes.Should().Be(before.Notes);
        practiceAfter.Repetitions.Should().Be(practiceBefore.Repetitions);
        practiceAfter.TechniqueId.Should().Be(practiceBefore.TechniqueId);
    }
}
