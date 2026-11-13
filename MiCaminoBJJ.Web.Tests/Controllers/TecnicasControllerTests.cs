using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Web.Controllers;
using MiCaminoBJJ.Web.Services;
using MiCaminoBJJ.Web.ViewModels;

namespace MiCaminoBJJ.Web.Tests.Controllers;

/// <summary>
/// Etapa 1 Web — comportamiento del TecnicasController (solo lectura, GET).
/// Cubre: listado, aplicación de filtros, conservación de parámetros GET en el
/// modelo y en las rutas de detalle/vuelta, detalle válido y HTTP 404.
/// </summary>
public class TecnicasControllerTests
{
    private readonly TecnicasDemoConsultaService _consulta = new();
    private readonly TecnicasController _sut;

    public TecnicasControllerTests()
    {
        _sut = new TecnicasController(_consulta, NullLogger<TecnicasController>.Instance);
    }

    private static TecnicasIndexViewModel EjecutarIndex(TecnicasController controller, string? buscar = null, int? cinturon = null, int? categoria = null, int? posicion = null)
    {
        var result = controller.Index(buscar, cinturon, categoria, posicion).Result;

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.Model.Should().BeOfType<TecnicasIndexViewModel>();
        return (TecnicasIndexViewModel)view.Model!;
    }

    // ── 1. Listado ───────────────────────────────────────────────────────

    [Fact]
    public async Task Index_SinParametros_DevuelveVistaConTodasLasTecnicas()
    {
        var model = EjecutarIndex(_sut);

        model.Tecnicas.Should().HaveCount(11);
        model.Cinturones.Should().NotBeEmpty();
        model.Categorias.Should().NotBeEmpty();
        model.Posiciones.Should().NotBeEmpty();
        model.TotalResultados.Should().Be(11);
        model.HayResultados.Should().BeTrue();
        model.MensajeError.Should().BeNull();
        model.EsFuenteDeDemostracion.Should().BeTrue();
    }

    // ── 2 y 3. Búsqueda por nombre, case-insensitive ─────────────────────

    [Theory]
    [InlineData("kimura")]
    [InlineData("KIMURA")]
    public async Task Index_ConBusqueda_FiltraPorNombre(string buscar)
    {
        var model = EjecutarIndex(_sut, buscar: buscar);

        model.Tecnicas.Should().Contain(t => t.Nombre == "Kimura desde guardia cerrada");
        model.Tecnicas.Should().NotContain(t => t.Nombre == "Mata león");
        model.Filtro.Buscar.Should().Be(buscar, "el parámetro GET debe conservarse en el modelo");
    }

    // ── 4. Búsqueda por alias ────────────────────────────────────────────

    [Fact]
    public async Task Index_ConBusquedaPorAlias_EncontraMataLeon()
    {
        var model = EjecutarIndex(_sut, buscar: "RNC");

        model.Tecnicas.Should().ContainSingle()
            .Which.Nombre.Should().Be("Mata león");
    }

    // ── 5 y 6. Filtros individuales y combinados ─────────────────────────

    [Fact]
    public async Task Index_ConFiltroDeCinturon_AplicaElFiltroYConservaElParametro()
    {
        var model = EjecutarIndex(_sut, cinturon: 2);

        model.Tecnicas.Should().HaveCount(4, "solo técnicas del cinturón Azul");
        model.Tecnicas.Should().OnlyContain(t => t.Cinturon == "Azul");
        model.Filtro.Cinturon.Should().Be(2);
        model.Filtro.TieneFiltros.Should().BeTrue();
    }

    [Fact]
    public async Task Index_SinFiltroDeCinturon_MuestraElCatalogoCompleto()
    {
        // Equivalente al clic en la tarjeta «Todos» del filtro visual.
        var model = EjecutarIndex(_sut, cinturon: null);

        model.Tecnicas.Should().HaveCount(11, "Todos vuelve al catálogo completo");
        model.Filtro.Cinturon.Should().BeNull();
        model.Filtro.TieneFiltros.Should().BeFalse();
    }

    [Fact]
    public async Task Index_LasOpcionesDeCinturon_IncluyenImagenYGlowParaTodosLosCinturones()
    {
        var model = EjecutarIndex(_sut);

        model.Cinturones.Should().HaveCount(5, "Blanco, Azul, Violeta, Marrón y Negro");
        model.Cinturones.Should().OnlyContain(o =>
                !string.IsNullOrEmpty(o.Imagen) && !string.IsNullOrEmpty(o.ClaseGlow),
            "cada tarjeta de filtro necesita su PNG transparente y su clase de glow");
        model.Cinturones.Select(o => o.ClaseGlow).Should().OnlyHaveUniqueItems(
            "cada cinturón tiene su propio color de glow");
        model.Cinturones.Select(o => o.Imagen).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Index_ConFiltrosCombinados_YBusqueda_AplicaTodos()
    {
        var model = EjecutarIndex(_sut, buscar: "triángulo", cinturon: 2, categoria: 4, posicion: 3);

        model.Tecnicas.Should().ContainSingle()
            .Which.Nombre.Should().Be("Triángulo desde guardia cerrada");

        // Los cuatro parámetros GET quedan reflejados en el modelo…
        model.Filtro.Buscar.Should().Be("triángulo");
        model.Filtro.Cinturon.Should().Be(2);
        model.Filtro.Categoria.Should().Be(4);
        model.Filtro.Posicion.Should().Be(3);

        // …y en la ruta del detalle, para poder volver sin perderlos.
        var ruta = model.RutaDetalle(model.Tecnicas[0].Id);
        ruta.Should().StartWith("/Tecnicas/Details/")
            .And.Contain("buscar=tri%C3%A1ngulo")
            .And.Contain("cinturon=2")
            .And.Contain("categoria=4")
            .And.Contain("posicion=3");
    }

    // ── 7. Resultado vacío ───────────────────────────────────────────────

    [Fact]
    public async Task Index_SinCoincidencias_DevuelveVaciaSinError()
    {
        var model = EjecutarIndex(_sut, buscar: "zzz no existe");

        model.Tecnicas.Should().BeEmpty();
        model.HayResultados.Should().BeFalse();
        model.TotalResultados.Should().Be(0);
        model.MensajeError.Should().BeNull("sin resultados no es un error técnico");
    }

    // ── 8. Detalle válido ────────────────────────────────────────────────

    [Fact]
    public async Task Details_IdValido_DevuelveElDetalle()
    {
        var result = await _sut.Details(2, buscar: null, cinturon: null, categoria: null, posicion: null);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<TecnicaDetalleViewModel>().Subject;

        model.Id.Should().Be(2);
        model.Nombre.Should().Be("Mata león");
        model.Cinturon.Should().Be("Blanco");
        model.Categoria.Should().Be("Sumisión");
        model.Posicion.Should().Be("Espalda");
        model.Pasos.Should().NotBeEmpty("Steps es JSON de la entidad y debe parsearse");
        model.Alias.Should().Contain("RNC");
        model.RutaVolver.Should().Be("/Tecnicas");
        model.EsFuenteDeDemostracion.Should().BeTrue();
    }

    [Fact]
    public async Task Details_ConParametrosGet_ConservaLosFiltrosEnLaRutaDeVuelta()
    {
        var result = await _sut.Details(2, buscar: "mata", cinturon: 1, categoria: 4, posicion: null);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<TecnicaDetalleViewModel>().Subject;

        model.RutaVolver.Should().Be("/Tecnicas?buscar=mata&cinturon=1&categoria=4");
    }

    [Fact]
    public async Task Details_SinDatosDeMultimedia_DejaLaListaVacia()
    {
        var result = await _sut.Details(1, null, null, null, null);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<TecnicaDetalleViewModel>().Subject;

        model.Recursos.Should().BeEmpty("la demo no inventa vídeos, imágenes ni enlaces");
    }

    // ── 9. ID inexistente → 404 ──────────────────────────────────────────

    [Fact]
    public async Task Details_IdInexistente_Devuelve404()
    {
        var result = await _sut.Details(999999, null, null, null, null);

        result.Should().BeOfType<NotFoundResult>();
    }

    // ── Manejo de errores de la fuente de datos ──────────────────────────

    [Fact]
    public async Task Index_SiLaFuenteFalla_MuestraMensajeYListadoVacio()
    {
        var roto = new TecnicasController(
            new FuenteRotaStub(),
            NullLogger<TecnicasController>.Instance);

        var model = EjecutarIndex(roto);

        model.Tecnicas.Should().BeEmpty();
        model.MensajeError.Should().NotBeNullOrWhiteSpace();
        model.MensajeError.Should().Contain("biblioteca");
    }

    /// <summary>Fuente que simula un fallo de consulta (p. ej. caída de SQL Server).</summary>
    private sealed class FuenteRotaStub : ITecnicasConsultaService
    {
        public bool EsFuenteDeDemostracion => false;

        public Task<IReadOnlyList<Core.Entities.Technique>> SearchAsync(TechniqueSearchFilter filter) =>
            throw new InvalidOperationException("No se pudo conectar con la base de datos.");

        public Task<Core.Entities.Technique?> GetByIdAsync(int id) =>
            throw new InvalidOperationException("No se pudo conectar con la base de datos.");

        public Task<Core.Entities.Technique?> GetByNombreOAliasExactoAsync(string? nombre) =>
            throw new InvalidOperationException("No se pudo conectar con la base de datos.");

        public Task<IReadOnlyList<Core.Entities.Belt>> GetBeltsAsync() =>
            throw new InvalidOperationException("No se pudo conectar con la base de datos.");

        public Task<IReadOnlyList<Core.Entities.TechniqueCategory>> GetCategoriesAsync() =>
            throw new InvalidOperationException("No se pudo conectar con la base de datos.");

        public Task<IReadOnlyList<Core.Entities.Position>> GetPositionsAsync() =>
            throw new InvalidOperationException("No se pudo conectar con la base de datos.");
    }
}
