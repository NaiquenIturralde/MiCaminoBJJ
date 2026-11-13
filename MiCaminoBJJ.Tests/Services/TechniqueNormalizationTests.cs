using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Infrastructure.Services;
using MiCaminoBJJ.Tests.Infrastructure;

namespace MiCaminoBJJ.Tests.Services;

/// <summary>
/// Integración: TechniqueService mantiene NameNormalized sincronizado
/// al crear y editar técnicas, y DataNormalizationService es idempotente.
/// </summary>
public class TechniqueNormalizationTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TechniqueService _techniqueService;
    private readonly DataNormalizationService _backfill;
    private readonly int _beltId;
    private readonly int _categoryId;
    private readonly int _positionId;

    public TechniqueNormalizationTests()
    {
        _db = new SqliteTestDatabase();

        // Etapa 1.2: TechniqueService exige un administrador autenticado y
        // registra auditoría; las pruebas de normalización usan una sesión fake.
        var session = new FakeSessionService
        {
            UserId = 1,
            Role = UserRole.Administrador
        };
        var audit = new AuditService(_db.Context, NullLogger<AuditService>.Instance);
        var authorization = new AdminAuthorizationService(session, audit);
        _techniqueService = new TechniqueService(_db.Context, audit, authorization);
        _backfill = new DataNormalizationService(_db.Context, NullLogger<DataNormalizationService>.Instance);

        var belt = new Belt { Name = "Blanco", DisplayOrder = 1 };
        var category = new TechniqueCategory { Name = "Sumisión", DisplayOrder = 0 };
        var position = new Position { Name = "Espalda", DisplayOrder = 0 };
        _db.Context.AddRange(belt, category, position);
        _db.Context.SaveChanges();

        _beltId = belt.Id;
        _categoryId = category.Id;
        _positionId = position.Id;
    }

    public void Dispose() => _db.Dispose();

    // ── Sincronización al crear/editar ──────────────────────────────────────

    [Fact]
    public async Task CreateAsync_SetsNameNormalized()
    {
        var technique = await _techniqueService.CreateAsync(
            "América", "d", "o", null, null, _beltId, _categoryId, _positionId);

        technique.NameNormalized.Should().Be("AMERICA");
        technique.NameNormalized.Should().Be(TextNormalizer.Normalize(technique.Name));
    }

    [Fact]
    public async Task UpdateAsync_KeepsNameNormalizedInSync()
    {
        var technique = await _techniqueService.CreateAsync(
            "Kimura", "d", "o", null, null, _beltId, _categoryId, _positionId);

        await _techniqueService.UpdateAsync(technique.Id,
            "llave americana", "d2", "o2", null, null, _beltId, _categoryId, _positionId);

        var stored = await _db.Context.Techniques.SingleAsync(t => t.Id == technique.Id);
        stored.Name.Should().Be("llave americana");
        stored.NameNormalized.Should().Be("LLAVE AMERICANA");
        stored.NameNormalized.Should().Be(TextNormalizer.Normalize(stored.Name));
    }

    [Fact]
    public async Task UpdateAsync_ChangingOnlyOtherFields_KeepsNormalized()
    {
        var technique = await _techniqueService.CreateAsync(
            "Mata león", "d", "o", null, null, _beltId, _categoryId, _positionId);

        await _techniqueService.UpdateAsync(technique.Id,
            technique.Name, "descripción nueva", "o2", null, null, _beltId, _categoryId, _positionId);

        var stored = await _db.Context.Techniques.SingleAsync(t => t.Id == technique.Id);
        stored.NameNormalized.Should().Be("MATA LEON");
    }

    // ── Backfill idempotente ────────────────────────────────────────────────

    [Fact]
    public async Task Backfill_FillsNullNormalizedNames_UsingTextNormalizerRule()
    {
        // Simula filas antiguas (previas a la etapa 2.2): NameNormalized null.
        _db.Execute("""
            INSERT INTO Techniques (Name, Description, Objective, BeltId, CategoryId, PositionId, IsActive, CreatedAt, NameNormalized)
            VALUES ('Óscar del Ñandú', 'd', 'o', $b, $c, $p, 1, '2026-01-01 00:00:00', NULL)
            """,
            ("$b", _beltId), ("$c", _categoryId), ("$p", _positionId));

        var updated = await _backfill.BackfillTechniqueNamesAsync();

        updated.Should().Be(1);
        var stored = await _db.Context.Techniques.SingleAsync(t => t.Name.StartsWith("Óscar"));
        stored.NameNormalized.Should().Be("OSCAR DEL NANDU");
        stored.NameNormalized.Should().Be(TextNormalizer.Normalize(stored.Name));
    }

    [Fact]
    public async Task Backfill_IsIdempotent_SecondRunUpdatesNothing()
    {
        _db.Execute("""
            INSERT INTO Techniques (Name, Description, Objective, BeltId, CategoryId, PositionId, IsActive, CreatedAt, NameNormalized)
            VALUES ('Triángulo', 'd', 'o', $b, $c, $p, 1, '2026-01-01 00:00:00', NULL)
            """,
            ("$b", _beltId), ("$c", _categoryId), ("$p", _positionId));

        var first = await _backfill.BackfillTechniqueNamesAsync();
        var second = await _backfill.BackfillTechniqueNamesAsync();
        var third = await _backfill.BackfillTechniqueNamesAsync();

        first.Should().Be(1);
        second.Should().Be(0);
        third.Should().Be(0);
    }

    [Fact]
    public async Task Backfill_DoesNotOverwriteExistingNormalizedValues()
    {
        var technique = await _techniqueService.CreateAsync(
            "Nombre Original", "d", "o", null, null, _beltId, _categoryId, _positionId);

        // Un valor ya normalizado no debe tocarse aunque sea "mejor" regenerarlo.
        await _backfill.BackfillTechniqueNamesAsync();

        var stored = await _db.Context.Techniques.SingleAsync(t => t.Id == technique.Id);
        stored.NameNormalized.Should().Be("NOMBRE ORIGINAL");
    }
}
