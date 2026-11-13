using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Infrastructure.Data.Seed;
using MiCaminoBJJ.Infrastructure.Services;
using MiCaminoBJJ.Tests.Infrastructure;

namespace MiCaminoBJJ.Tests.Infrastructure;

/// <summary>
/// Etapa 2.2.1: verificación del orden Migrate → Seed → Backfill en DatabaseInitializer.
/// Ejecuta la secuencia seed-then-backfill (sin MigrateAsync, que no aplica a la base
/// de prueba ya creada) para demostrar que el nuevo orden preserva seguridad e idempotencia.
/// </summary>
public class DatabaseInitializerOrderTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly SeedDataService _seed;
    private readonly DataNormalizationService _backfill;

    public DatabaseInitializerOrderTests()
    {
        _db = new SqliteTestDatabase();

        var passwordHasher = new PasswordService();
        _seed = new SeedDataService(_db.Context, passwordHasher, NullLogger<SeedDataService>.Instance);
        _backfill = new DataNormalizationService(_db.Context, NullLogger<DataNormalizationService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SeedThenBackfill_AllTechniquesEndNormalized()
    {
        // 1) Seed: crea cinturones, categorías, posiciones y técnicas (ya normalizadas).
        await _seed.SeedAsync();

        // 2) Técnica "legada" (previa a 2.2) con NameNormalized null, como en una base real.
        var beltId = await _db.Context.Belts.Select(b => b.Id).FirstAsync();
        var categoryId = await _db.Context.TechniqueCategories.Select(c => c.Id).FirstAsync();
        var positionId = await _db.Context.Positions.Select(p => p.Id).FirstAsync();

        _db.Execute("""
            INSERT INTO Techniques (Name, Description, Objective, BeltId, CategoryId, PositionId, IsActive, CreatedAt, NameNormalized)
            VALUES ('Óscar del Ñandú', 'd', 'o', $b, $c, $p, 1, '2026-01-01 00:00:00', NULL)
            """,
            ("$b", beltId), ("$c", categoryId), ("$p", positionId));

        // 3) Backfill (paso que corre después del seed en el inicializador).
        var updated = await _backfill.BackfillTechniqueNamesAsync();

        updated.Should().Be(1, "solo la técnica legada debe quedar pendiente");

        var techniques = await _db.Context.Techniques.ToListAsync();
        techniques.Should().NotBeEmpty(); // el seed efectivamente corrió
        foreach (var t in techniques)
            t.NameNormalized.Should().Be(TextNormalizer.Normalize(t.Name),
                $"la técnica '{t.Name}' debe quedar normalizada con la regla de TextNormalizer");
    }

    [Fact]
    public async Task SeedThenBackfill_IsIdempotentOnSecondRun()
    {
        await _seed.SeedAsync();
        var first = await _backfill.BackfillTechniqueNamesAsync();
        var second = await _backfill.BackfillTechniqueNamesAsync();

        first.Should().Be(0, "el seed ya setea NameNormalized: no debe haber nada pendiente");
        second.Should().Be(0);

        // El seed re-ejecutado no duplica datos (guard de Belt).
        var beltsBefore = await _db.Context.Belts.CountAsync();
        await _seed.SeedAsync();
        var beltsAfter = await _db.Context.Belts.CountAsync();

        beltsAfter.Should().Be(beltsBefore);
    }

    [Fact]
    public async Task BackfillAfterSeed_DoesNotModifyExistingNormalizedNames()
    {
        await _seed.SeedAsync();
        var all = await _db.Context.Techniques.ToListAsync();
        var snapshot = all.ToDictionary(t => t.Id, t => (t.Name, t.NameNormalized));

        await _backfill.BackfillTechniqueNamesAsync();

        foreach (var t in await _db.Context.Techniques.ToListAsync())
        {
            t.Name.Should().Be(snapshot[t.Id].Name);
            t.NameNormalized.Should().Be(snapshot[t.Id].NameNormalized,
                "el backfill solo toca filas null/vacío: no reescribe valores existentes");
        }
    }
}
