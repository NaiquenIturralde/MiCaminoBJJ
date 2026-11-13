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
/// Integración: TechniqueSearchService sobre SQLite en memoria real.
/// Cubre nombre original, sinónimo, subcadena, tildes, mayúsculas, ñ/n y filtros.
/// </summary>
public class TechniqueSearchServiceTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TechniqueSearchService _sut;
    private readonly AliasService _aliasService;

    private int _brazilianBeltId;
    private int _whiteBeltId;
    private int _submissionCategoryId;
    private int _escapeCategoryId;
    private int _backPositionId;
    private int _mountPositionId;
    private int _mataLeonId;
    private int _armbarId;
    private int _escapeMontadaId;

    public TechniqueSearchServiceTests()
    {
        _db = new SqliteTestDatabase();
        _sut = new TechniqueSearchService(_db.Context);
        _aliasService = new AliasService(_db.Context, NullLogger<AliasService>.Instance);

        Seed();
    }

    public void Dispose() => _db.Dispose();

    private void Seed()
    {
        var brazilian = new Belt { Name = "Brazilian", DisplayOrder = 0 };
        var white = new Belt { Name = "Blanco", DisplayOrder = 1 };
        var submission = new TechniqueCategory { Name = "Sumisión", DisplayOrder = 0 };
        var escape = new TechniqueCategory { Name = "Escape", DisplayOrder = 1 };
        var back = new Position { Name = "Espalda", DisplayOrder = 0 };
        var mount = new Position { Name = "Montada", DisplayOrder = 1 };

        _db.Context.AddRange(brazilian, white, submission, escape, back, mount);
        _db.Context.SaveChanges();

        _brazilianBeltId = brazilian.Id;
        _whiteBeltId = white.Id;
        _submissionCategoryId = submission.Id;
        _escapeCategoryId = escape.Id;
        _backPositionId = back.Id;
        _mountPositionId = mount.Id;

        var mataLeon = new Technique
        {
            Name = "Mata león",
            NameNormalized = TextNormalizer.Normalize("Mata león"),
            Description = "Estrangulación desde la espalda",
            Objective = "Estrangular",
            BeltId = white.Id,
            CategoryId = submission.Id,
            PositionId = back.Id
        };
        var armbar = new Technique
        {
            Name = "Armbar desde guardia cerrada",
            NameNormalized = TextNormalizer.Normalize("Armbar desde guardia cerrada"),
            Description = "Sumisión de brazo",
            Objective = "Aislar el brazo",
            BeltId = brazilian.Id,
            CategoryId = submission.Id,
            PositionId = mount.Id
        };
        var escapeMontada = new Technique
        {
            Name = "Escape de montada",
            NameNormalized = TextNormalizer.Normalize("Escape de montada"),
            Description = "Puente y rodilla",
            Objective = "Desequilibrar",
            BeltId = white.Id,
            CategoryId = escape.Id,
            PositionId = mount.Id
        };

        _db.Context.AddRange(mataLeon, armbar, escapeMontada);
        _db.Context.SaveChanges();

        _mataLeonId = mataLeon.Id;
        _armbarId = armbar.Id;
        _escapeMontadaId = escapeMontada.Id;
    }

    private Task<List<Technique>> SearchAsync(string? term, int? beltId = null, int? categoryId = null, int? positionId = null)
        => _sut.SearchAsync(new TechniqueSearchFilter
        {
            SearchTerm = term,
            BeltId = beltId,
            CategoryId = categoryId,
            PositionId = positionId
        });

    // ── Nombre original ─────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ByName_FindsTechnique()
    {
        var result = await SearchAsync("Armbar");
        result.Should().ContainSingle(t => t.Id == _armbarId);
    }

    [Fact]
    public async Task Search_ByNameIsPartial_FindsTechnique()
    {
        var result = await SearchAsync("montad");
        result.Should().ContainSingle(t => t.Id == _escapeMontadaId);
    }

    // ── Tildes y mayúsculas ─────────────────────────────────────────────────

    [Theory]
    [InlineData("mata león")]
    [InlineData("MATA LEÓN")]
    [InlineData("Mata Leon")]
    [InlineData("mATA LEO\u00D1")]
    public async Task Search_AccentsAndCase_AreTolerated(string term)
    {
        var result = await SearchAsync(term);
        result.Should().ContainSingle(t => t.Id == _mataLeonId);
    }

    [Fact]
    public async Task Search_WithoutAccents_MatchesAccentedName()
    {
        var result = await SearchAsync("MATA LEON");
        result.Should().ContainSingle(t => t.Id == _mataLeonId);
    }

    // ── Ñ / n (según TextNormalizer) ────────────────────────────────────────

    [Theory]

    [InlineData("LEO\u00D1")]   // con ñ
    [InlineData("LEON")]        // sin ñ → misma forma normalizada
    public async Task Search_SubstringWithENye_ToleratesBothForms(string term)
    {
        var result = await SearchAsync(term);
        result.Should().ContainSingle(t => t.Id == _mataLeonId);
    }

    // ── Sinónimos (alias) ───────────────────────────────────────────────────

    [Fact]
    public async Task Search_ByAlias_FindsTechnique()
    {
        await _aliasService.AddAsync(_mataLeonId, "Rear Naked Choke");

        var result = await SearchAsync("rear naked");
        result.Should().ContainSingle(t => t.Id == _mataLeonId);
    }

    [Fact]
    public async Task Search_ByAliasWithDifferentAccentsAndCase_FindsTechnique()
    {
        await _aliasService.AddAsync(_mataLeonId, "Estrangulación Trasera");

        var result = await SearchAsync("ESTRANGULACION trasera");
        result.Should().ContainSingle(t => t.Id == _mataLeonId);
    }

    [Fact]
    public async Task Search_TechniqueNameStillWorks_WhenItHasAliases()
    {
        await _aliasService.AddAsync(_mataLeonId, "RNC");

        (await SearchAsync("mata")).Should().ContainSingle(t => t.Id == _mataLeonId);
    }

    // ── Búsqueda literal conservada (descripción) ───────────────────────────

    [Fact]
    public async Task Search_ByDescriptionTerm_StillWorks()
    {
        var result = await SearchAsync("Estrangulación desde la espalda");
        result.Should().ContainSingle(t => t.Id == _mataLeonId);
    }

    // ── Filtros combinados ──────────────────────────────────────────────────

    [Fact]
    public async Task Search_FilterByBelt_NarrowsResults()
    {
        var result = await SearchAsync(null, beltId: _brazilianBeltId);
        result.Should().ContainSingle(t => t.Id == _armbarId);
    }

    [Fact]
    public async Task Search_FilterByCategory_ComposesWithTerm()
    {
        var result = await SearchAsync("escape", categoryId: _escapeCategoryId);
        result.Should().ContainSingle(t => t.Id == _escapeMontadaId);

        var empty = await SearchAsync("escape", categoryId: _submissionCategoryId);
        empty.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_FilterByPosition_ComposesWithTerm()
    {
        var result = await SearchAsync("mata", positionId: _backPositionId);
        result.Should().ContainSingle(t => t.Id == _mataLeonId);

        var empty = await SearchAsync("mata", positionId: _mountPositionId);
        empty.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_WithoutTerm_ReturnsAllActive()
    {
        var result = await SearchAsync(null);
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Search_BlankTerm_ReturnsAllActive()
    {
        var result = await SearchAsync("   ");
        result.Should().HaveCount(3);
    }

    // ── Caracteres especiales: %, _ y \ deben interpretarse LITERALMENTE ────

    private async Task<int> AddTechniqueAsync(string name, string description = "descripción de prueba")
    {
        var technique = new Technique
        {
            Name = name,
            NameNormalized = TextNormalizer.Normalize(name),
            Description = description,
            Objective = "o",
            BeltId = _whiteBeltId,
            CategoryId = _escapeCategoryId,
            PositionId = _mountPositionId
        };
        _db.Context.Techniques.Add(technique);
        await _db.Context.SaveChangesAsync();
        return technique.Id;
    }

    [Fact]
    public async Task Search_PercentSign_IsLiteral_NotSqlWildcard()
    {
        // Si % se interpretara como comodín, "100%" matchearía también "1000".
        var literalId = await AddTechniqueAsync("Registro 100% libres");
        await AddTechniqueAsync("Registro 1000 libres"); // decoy

        var result = await SearchAsync("100%");

        result.Should().ContainSingle(t => t.Id == literalId,
            "el % debe tratarse literalmente y no como comodín LIKE");
    }

    [Fact]
    public async Task Search_OnlyPercent_MatchesOnlyTechniquesContainingLiteralPercent()
    {
        var literalId = await AddTechniqueAsync("Barra 100% colgante");

        var result = await SearchAsync("%");

        // Con comodín sin escapar, "%" matchearía TODAS las técnicas (y todas las descripciones).
        result.Should().ContainSingle(t => t.Id == literalId);
    }

    [Fact]
    public async Task Search_Underscore_IsLiteral_NotSingleCharWildcard()
    {
        // Si _ se interpretara como comodín, "guardia_pass" matchearía "guardiaXpass".
        var literalId = await AddTechniqueAsync("guardia_pass cerrada");
        await AddTechniqueAsync("guardiaXpass cerrada"); // decoy

        var result = await SearchAsync("guardia_pass");

        result.Should().ContainSingle(t => t.Id == literalId,
            "el _ debe tratarse literalmente y no como comodín de un carácter");
    }

    [Fact]
    public async Task Search_Backslash_IsLiteralAndDoesNotThrow()
    {
        // \\ como escape de LIKE podría generar "escape sequence" inválida o matchear de más.
        var literalId = await AddTechniqueAsync("Camino\\Raro"); // Camino\Raro

        var result = await SearchAsync("Camino\\Raro"); // término "Camino\Raro"

        result.Should().ContainSingle(t => t.Id == literalId);
    }

    [Fact]
    public async Task Search_ContainsTranslation_UsesLiteralMatching()
    {
        // Documenta la traducción REAL de EF Core: si no hay ESCAPE/instr/replace,
        // los comodines del parámetro viajarían sin escapar a LIKE (ver tests anteriores).
        var term = TextNormalizer.Normalize("100%");
        var sql = _db.Context.Techniques
            .Where(t => t.NameNormalized != null && t.NameNormalized.Contains(term))
            .ToQueryString();

        var isLiteral =
            sql.Contains("instr", StringComparison.OrdinalIgnoreCase) ||
            sql.Contains("ESCAPE", StringComparison.OrdinalIgnoreCase) ||
            sql.Contains("replace", StringComparison.OrdinalIgnoreCase);

        isLiteral.Should().BeTrue($"la traducción debe escapar o usar instr; SQL generado: {sql}");
    }

    // ── Normalización del servicio ──────────────────────────────────────────

    [Fact]
    public async Task Search_NameNormalizedMatchesTextNormalizerRule()
    {
        // La columna debe reflejar EXACTAMENTE TextNormalizer.Normalize(Name).
        var techniques = await _db.Context.Techniques.ToListAsync();
        foreach (var t in techniques)
            t.NameNormalized.Should().Be(TextNormalizer.Normalize(t.Name));
    }
}
