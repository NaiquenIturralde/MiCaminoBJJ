using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Core.Services;
using MiCaminoBJJ.Infrastructure.Services;
using MiCaminoBJJ.Tests.Infrastructure;

namespace MiCaminoBJJ.Tests.Services;

/// <summary>
/// Integración: AliasService sobre SQLite en memoria real.
/// Cubre normalización al persistir, duplicados (índice único) y eliminación.
/// </summary>
public class AliasServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly AliasService _sut;
    private readonly int _techniqueId;
    private readonly int _otherTechniqueId;

    public AliasServiceTests()
    {
        _db = new SqliteTestDatabase();
        _sut = new AliasService(_db.Context, NullLogger<AliasService>.Instance);

        var belt = new Belt { Name = "Blanco", DisplayOrder = 1 };
        var category = new TechniqueCategory { Name = "Sumisión", DisplayOrder = 0 };
        var position = new Position { Name = "Espalda", DisplayOrder = 0 };
        _db.Context.AddRange(belt, category, position);
        _db.Context.SaveChanges();

        var mataLeon = new Technique
        {
            Name = "Mata león",
            NameNormalized = TextNormalizer.Normalize("Mata león"),
            Description = "d",
            Objective = "o",
            BeltId = belt.Id,
            CategoryId = category.Id,
            PositionId = position.Id
        };
        var armbar = new Technique
        {
            Name = "Armbar",
            NameNormalized = TextNormalizer.Normalize("Armbar"),
            Description = "d",
            Objective = "o",
            BeltId = belt.Id,
            CategoryId = category.Id,
            PositionId = position.Id
        };
        _db.Context.AddRange(mataLeon, armbar);
        _db.Context.SaveChanges();

        _techniqueId = mataLeon.Id;
        _otherTechniqueId = armbar.Id;
    }

    public void Dispose() => _db.Dispose();

    // ── Normalización al persistir ──────────────────────────────────────────

    [Fact]
    public async Task Add_NormalizesName_KeepingReadableOriginal()
    {
        var alias = await _sut.AddAsync(_techniqueId, "  estrangulación trasera  ");

        alias.Name.Should().Be("estrangulación trasera");           // legible, solo trim
        alias.NameNormalized.Should().Be("ESTRANGULACION TRASERA"); // TextNormalizer

        var stored = await _db.Context.TechniqueAliases.SingleAsync(a => a.Id == alias.Id);
        stored.NameNormalized.Should().Be(TextNormalizer.Normalize("Estrangulación Trasera"));
    }

    // ── Duplicados ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_DuplicateNormalized_ThrowsWithClearMessage()
    {
        await _sut.AddAsync(_techniqueId, "Estrangulación Trasera");

        var act = async () => await _sut.AddAsync(_techniqueId, "ESTRANGULACION TRASERA");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*alias equivalente*");
    }

    [Fact]
    public async Task Add_DuplicateCaseInsensitiveAscii_Throws()
    {
        await _sut.AddAsync(_techniqueId, "Rear Naked Choke");

        var act = async () => await _sut.AddAsync(_techniqueId, "rear naked choke");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Add_SameAliasOnDifferentTechnique_IsAllowed()
    {
        // El duplicado es POR TÉCNICA (índice compuesto), no global.
        var first = await _sut.AddAsync(_techniqueId, "RNC");
        var second = await _sut.AddAsync(_otherTechniqueId, "RNC");

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public async Task Add_EnforcesUniqueIndexAtDatabaseLevel()
    {
        // Insert directo que salta la validación del servicio: el índice único debe rechazarlo.
        _db.Context.TechniqueAliases.Add(new TechniqueAlias
        {
            TechniqueId = _techniqueId,
            Name = "otro",
            NameNormalized = "RNC" // colisiona con el alias previo normalizado
        });
        _db.Context.TechniqueAliases.Add(new TechniqueAlias
        {
            TechniqueId = _techniqueId,
            Name = "otro2",
            NameNormalized = "RNC"
        });

        var act = () => _db.Context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Add_EmptyOrWhitespace_Throws()
    {
        var act = async () => await _sut.AddAsync(_techniqueId, "   ");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Add_NullName_ThrowsArgumentException()
    {
        var act = async () => await _sut.AddAsync(_techniqueId, null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Add_EmptyString_ThrowsArgumentException()
    {
        var act = async () => await _sut.AddAsync(_techniqueId, "");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Add_NameNormalizingToEmpty_ThrowsArgumentException()
    {
        // Solo marcas diacríticas combinantes: no es whitespace, pero TextNormalizer → "".
        var combiningMarksOnly = "\u0301\u0301\u0301";

        var act = async () => await _sut.AddAsync(_techniqueId, combiningMarksOnly);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*caracteres significativos*");
    }

    [Fact]
    public async Task Add_NameLongerThanLimit_ThrowsArgumentException()
    {
        // Límite existente: HasMaxLength(200) en TechniqueAliasConfiguration.
        var tooLong = new string('a', 201);

        var act = async () => await _sut.AddAsync(_techniqueId, tooLong);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*200*");
    }

    [Fact]
    public async Task Add_NameExactlyAtLimit_IsAccepted()
    {
        var atLimit = new string('a', 200);

        var alias = await _sut.AddAsync(_techniqueId, atLimit);

        alias.Name.Should().HaveLength(200);
    }

    // ── Distinción real de errores SQLite (índice único vs otros constraints) ──

    [Fact]
    public async Task Add_UniqueConstraintViolationFromRace_ThrowsDuplicateMessage()
    {
        // Fila pendiente en el tracker (invisible para la consulta AnyAsync previa)
        // simula una carrera: la validación previa pasa y el ÍNDICE ÚNICO falla en SaveChanges.
        _db.Context.TechniqueAliases.Add(new TechniqueAlias
        {
            TechniqueId = _techniqueId,
            Name = "previo",
            NameNormalized = "RNC"
        });

        var act = async () => await _sut.AddAsync(_techniqueId, "rnc"); // normaliza → RNC

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("alias equivalente");
        // Cadena: InvalidOperationException → DbUpdateException → SqliteException (UNIQUE).
        ex.Which.InnerException.Should().BeOfType<DbUpdateException>();
        var dbEx = (DbUpdateException)ex.Which.InnerException!;
        dbEx.InnerException.Should().BeOfType<Microsoft.Data.Sqlite.SqliteException>();
        AliasService.IsUniqueConstraintViolation(dbEx).Should().BeTrue();
    }

    [Fact]
    public async Task Add_ForeignKeyViolation_IsNotReportedAsDuplicate()
    {
        // Fila pendiente con FK inexistente: SaveChanges falla con SQLITE_CONSTRAINT_FOREIGNKEY
        // (787), que NO debe traducirse como "alias duplicado".
        _db.Context.TechniqueAliases.Add(new TechniqueAlias
        {
            TechniqueId = 999999, // FK inválida
            Name = "huérfano",
            NameNormalized = "HUERFANO"
        });

        var act = async () => await _sut.AddAsync(_techniqueId, "nombre nuevo");

        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        ex.Which.ToString().Should().NotContain("alias equivalente",
            "los errores FK no deben convertirse en mensajes de duplicados");
        // El filtro de códigos lo confirma: FK (787) jamás clasifica como único.
        AliasService.IsUniqueConstraintViolation(ex.Which).Should().BeFalse();
    }

    [Fact]
    public async Task Add_UnknownTechnique_ThrowsKeyNotFound()
    {
        var act = async () => await _sut.AddAsync(9999, "algo");
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Alta y baja ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ReturnsAliasesOrderedByName()
    {
        await _sut.AddAsync(_techniqueId, "zeta");
        await _sut.AddAsync(_techniqueId, "alfa");

        var aliases = await _sut.GetByTechniqueAsync(_techniqueId);

        aliases.Should().HaveCount(2);
        aliases.Select(a => a.Name).Should().Equal("alfa", "zeta");
    }

    [Fact]
    public async Task Remove_ExistingAlias_ReturnsTrue()
    {
        var alias = await _sut.AddAsync(_techniqueId, "RNC");

        (await _sut.RemoveAsync(alias.Id)).Should().BeTrue();
        (await _sut.GetByTechniqueAsync(_techniqueId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Remove_UnknownAlias_ReturnsFalse()
    {
        (await _sut.RemoveAsync(9999)).Should().BeFalse();
    }
}
