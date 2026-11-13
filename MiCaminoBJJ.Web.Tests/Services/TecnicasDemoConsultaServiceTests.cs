using FluentAssertions;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Web.Services;

namespace MiCaminoBJJ.Web.Tests.Services;

/// <summary>
/// Etapa 1 Web — biblioteca de técnicas sobre la fuente EN MEMORIA de
/// demostración (sin SQLite, sin SQL Server, sin migraciones).
/// Cubre: listado, búsqueda por nombre/alias (case- y tilde-insensible),
/// filtros individuales y combinados, resultado vacío y detalle por ID.
/// </summary>
public class TecnicasDemoConsultaServiceTests
{
    private readonly TecnicasDemoConsultaService _sut = new();

    private async Task<List<string>> NombresAsync(TechniqueSearchFilter filter) =>
        (await _sut.SearchAsync(filter)).Select(t => t.Name).ToList();

    // ── 1. Listado correcto ──────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_SinFiltros_DevuelveTodoElCatalogoDeDemostracion()
    {
        var tecnicas = await _sut.SearchAsync(new TechniqueSearchFilter());

        tecnicas.Should().HaveCount(11);
        tecnicas.Should().OnlyContain(t => t.IsActive, "el listado público solo muestra técnicas activas");
        tecnicas.Select(t => t.Belt.DisplayOrder).Should().BeInAscendingOrder(
            "el orden del dominio es por cinturón y luego por nombre");
    }

    [Fact]
    public async Task GetOpcionesAsync_DevuelveLosValoresDelDominio()
    {
        (await _sut.GetBeltsAsync()).Select(b => b.Name)
            .Should().Equal("Blanco", "Azul", "Violeta", "Marrón", "Negro");
        (await _sut.GetCategoriesAsync()).Select(c => c.Name)
            .Should().Equal("Defensa", "Escape", "Guardia", "Sumisión", "Control");
        (await _sut.GetPositionsAsync()).Select(p => p.Name)
            .Should().Equal("Montada", "Espalda", "Guardia cerrada", "Control lateral", "Media guardia");
    }

    [Fact]
    public async Task EsFuenteDeDemostracion_EsTrue()
        => _sut.EsFuenteDeDemostracion.Should().BeTrue("la interfaz debe etiquetar los datos de demostración");

    // ── 2 y 3. Búsqueda por nombre, insensible a mayúsculas ──────────────

    [Fact]
    public async Task SearchAsync_PorNombre_DevuelveSoloCoincidencias()
    {
        var nombres = await NombresAsync(new TechniqueSearchFilter { SearchTerm = "kimura" });

        nombres.Should().Contain("Kimura desde guardia cerrada");
        nombres.Should().Contain("Defensa de kimura en control lateral");
        nombres.Should().NotContain("Mata león");
    }

    [Theory]
    [InlineData("KIMURA")]
    [InlineData("KiMuRa")]
    [InlineData("kimura")]
    public async Task SearchAsync_PorNombre_EsInsensibleAMayusculas(string termino)
    {
        var nombres = await NombresAsync(new TechniqueSearchFilter { SearchTerm = termino });

        nombres.Should().BeEquivalentTo(new[]
        {
            "Kimura desde guardia cerrada",
            "Defensa de kimura en control lateral"
        });
    }

    [Theory]
    [InlineData("triangulo")] // sin tilde → "Triángulo desde guardia cerrada"
    [InlineData("TRIÁNGULO")]
    public async Task SearchAsync_PorNombre_EsInsensibleATildes(string termino)
    {
        var nombres = await NombresAsync(new TechniqueSearchFilter { SearchTerm = termino });

        nombres.Should().ContainSingle().Which.Should().Be("Triángulo desde guardia cerrada");
    }

    // ── 4. Búsqueda por alias / sinónimo ─────────────────────────────────

    [Theory]
    [InlineData("RNC")]
    [InlineData("Rear Naked Choke")]
    [InlineData("estrangulacion trasera")] // alias con tilde buscado sin tilde
    public async Task SearchAsync_PorAlias_DevuelveLaTecnica(string alias)
    {
        var nombres = await NombresAsync(new TechniqueSearchFilter { SearchTerm = alias });

        nombres.Should().Contain("Mata león");
    }

    [Fact]
    public async Task SearchAsync_PorAlias_DevuelvePassDeRodilla()
    {
        var nombres = await NombresAsync(new TechniqueSearchFilter { SearchTerm = "knee cut" });

        nombres.Should().ContainSingle().Which.Should().Be("Pass de corte de rodilla");
    }

    // ── 5. Filtros individuales ──────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_FiltroPorCinturon_DevuelveSoloEseCinturon()
    {
        var tecnicas = await _sut.SearchAsync(new TechniqueSearchFilter { BeltId = 3 });

        tecnicas.Should().ContainSingle();
        tecnicas[0].Name.Should().Be("Puente y giro desde control lateral");
        tecnicas.Should().OnlyContain(t => t.BeltId == 3);
    }

    [Fact]
    public async Task SearchAsync_FiltroPorCinturonNegro_DevuelveVacioSinRomperLaConsulta()
    {
        // Negro existe como cinturón del catálogo (para el filtro visual) pero
        // todavía no tiene técnicas cargadas: el filtro debe responder vacío.
        (await _sut.GetBeltsAsync()).Should().Contain(b => b.Name == "Negro");

        var tecnicas = await _sut.SearchAsync(new TechniqueSearchFilter { BeltId = 5 });

        tecnicas.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_FiltroPorCategoria_DevuelveSoloEsaCategoria()
    {
        var tecnicas = await _sut.SearchAsync(new TechniqueSearchFilter { CategoryId = 5 });

        tecnicas.Should().ContainSingle();
        tecnicas[0].Name.Should().Be("Pass de corte de rodilla");
        tecnicas.Should().OnlyContain(t => t.CategoryId == 5);
    }

    [Fact]
    public async Task SearchAsync_FiltroPorPosicion_DevuelveSoloEsaPosicion()
    {
        var tecnicas = await _sut.SearchAsync(new TechniqueSearchFilter { PositionId = 2 });

        tecnicas.Should().ContainSingle();
        tecnicas[0].Name.Should().Be("Mata león");
        tecnicas.Should().OnlyContain(t => t.PositionId == 2);
    }

    // ── 6. Combinación de filtros ────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_FiltrosCombinados_SeAplicanConY()
    {
        var nombres = await NombresAsync(new TechniqueSearchFilter
        {
            BeltId = 2,        // Azul
            CategoryId = 4,    // Sumisión
            PositionId = 3     // Guardia cerrada
        });

        nombres.Should().BeEquivalentTo(new[]
        {
            "Kimura desde guardia cerrada",
            "Triángulo desde guardia cerrada"
        });
    }

    [Fact]
    public async Task SearchAsync_BusquedaMasFiltros_AplicaAmbos()
    {
        var nombres = await NombresAsync(new TechniqueSearchFilter
        {
            SearchTerm = "triángulo",
            BeltId = 2,
            CategoryId = 4,
            PositionId = 3
        });

        nombres.Should().ContainSingle().Which.Should().Be("Triángulo desde guardia cerrada");
    }

    // ── 7. Resultado vacío ───────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_SinCoincidencias_DevuelveListaVacia()
    {
        var tecnicas = await _sut.SearchAsync(new TechniqueSearchFilter { SearchTerm = "zzz técnica inexistente" });

        tecnicas.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_FiltroQueNoCoincide_DevuelveListaVacia()
    {
        var tecnicas = await _sut.SearchAsync(new TechniqueSearchFilter { BeltId = 4, SearchTerm = "kimura" });

        // "Kimura desde guardia cerrada" es Azul: con Marrón no debe aparecer.
        tecnicas.Should().BeEmpty();
    }

    // ── 8 y 9. Detalle por ID e ID inexistente ───────────────────────────

    [Fact]
    public async Task GetByIdAsync_IdValido_DevuelveLaTecnicaConSusRelaciones()
    {
        var tecnica = await _sut.GetByIdAsync(2);

        tecnica.Should().NotBeNull();
        tecnica!.Name.Should().Be("Mata león");
        tecnica.Belt.Name.Should().Be("Blanco");
        tecnica.Category.Name.Should().Be("Sumisión");
        tecnica.Position.Name.Should().Be("Espalda");
        tecnica.Aliases.Should().Contain(a => a.Name == "RNC");
        tecnica.Steps.Should().NotBeNullOrWhiteSpace("los pasos existen en el dominio");
    }

    [Fact]
    public async Task GetByIdAsync_IdInexistente_DevuelveNull()
        => (await _sut.GetByIdAsync(999999)).Should().BeNull();
}
