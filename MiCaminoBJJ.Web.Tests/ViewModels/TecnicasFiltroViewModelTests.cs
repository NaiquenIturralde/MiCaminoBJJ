using FluentAssertions;
using MiCaminoBJJ.Web.ViewModels;

namespace MiCaminoBJJ.Web.Tests.ViewModels;

/// <summary>
/// Etapa 1 Web — conservación de los parámetros GET de la biblioteca.
/// Los filtros deben viajar en la URL (listado, detalle y vuelta) sin perderse
/// entre cada navegación.
/// </summary>
public class TecnicasFiltroViewModelTests
{
    [Fact]
    public void ToQueryString_SinFiltros_DevuelveVacio()
    {
        var filtro = new TecnicasFiltroViewModel();

        filtro.TieneFiltros.Should().BeFalse();
        filtro.ToQueryString().Should().BeEmpty();
        filtro.ToRutaListado().Should().Be("/Tecnicas");
        filtro.ToRutaDetalle(7).Should().Be("/Tecnicas/Details/7");
    }

    [Fact]
    public void ToQueryString_ConservaYCodificaTodosLosParametros()
    {
        var filtro = new TecnicasFiltroViewModel
        {
            Buscar = "mata león",
            Cinturon = 1,
            Categoria = 4,
            Posicion = 2
        };

        filtro.TieneFiltros.Should().BeTrue();
        filtro.ToQueryString().Should().Be("?buscar=mata%20le%C3%B3n&cinturon=1&categoria=4&posicion=2");
    }

    [Fact]
    public void ToQueryString_IgnoraBusquedaVaciaYSoloAplicaLosPresentes()
    {
        var filtro = new TecnicasFiltroViewModel { Buscar = "   ", Categoria = 5 };

        filtro.ToQueryString().Should().Be("?categoria=5");
    }

    [Fact]
    public void RutaConCinturon_CambiaElCinturonYConservaLosDemasFiltros()
    {
        var filtro = new TecnicasFiltroViewModel
        {
            Buscar = "kimura",
            Cinturon = 1,
            Categoria = 4,
            Posicion = 2
        };

        filtro.RutaConCinturon(3).Should().Be("/Tecnicas?buscar=kimura&cinturon=3&categoria=4&posicion=2");
    }

    [Fact]
    public void RutaConCinturon_Nulo_EsTodosSinPerderLaBusqueda()
    {
        var filtro = new TecnicasFiltroViewModel { Buscar = "rnc", Cinturon = 2 };

        filtro.RutaConCinturon(null).Should().Be("/Tecnicas?buscar=rnc",
            "la tarjeta «Todos» quita el cinturón pero conserva lo demás");
    }

    [Fact]
    public void RutaConCinturon_SinFiltros_EsElListadoLimpio()
    {
        var filtro = new TecnicasFiltroViewModel();

        filtro.RutaConCinturon(null).Should().Be("/Tecnicas");
        filtro.RutaConCinturon(4).Should().Be("/Tecnicas?cinturon=4");
    }

    [Fact]
    public void ToRutaDetalle_ConservaLosFiltrosAplicados()
    {
        var filtro = new TecnicasFiltroViewModel { Buscar = "kimura", Cinturon = 2 };

        filtro.ToRutaDetalle(6).Should().Be("/Tecnicas/Details/6?buscar=kimura&cinturon=2");
    }

    [Fact]
    public void ToRutaListado_ConservaLosFiltrosAplicados()
    {
        var filtro = new TecnicasFiltroViewModel { Buscar = "rnc", Categoria = 4, Posicion = 2 };

        filtro.ToRutaListado().Should().Be("/Tecnicas?buscar=rnc&categoria=4&posicion=2");
    }

    [Fact]
    public void ToSearchFilter_MapeaLosParametrosAlDominio()
    {
        var filtro = new TecnicasFiltroViewModel
        {
            Buscar = "triángulo",
            Cinturon = 2,
            Categoria = 4,
            Posicion = 3
        };

        var dominio = filtro.ToSearchFilter();

        dominio.SearchTerm.Should().Be("triángulo");
        dominio.BeltId.Should().Be(2);
        dominio.CategoryId.Should().Be(4);
        dominio.PositionId.Should().Be(3);
    }

    [Fact]
    public void RoundTrip_LosParametrosGetSeConservanDeIdaYVuelta()
    {
        // Simula: GET /Tecnicas?buscar=...&categoria=... → modelo → ruta de vuelta.
        var aplicado = new TecnicasFiltroViewModel { Buscar = "raspado", Categoria = 3 };
        var desdeListado = aplicado.ToRutaListado();

        desdeListado.Should().Be("/Tecnicas?buscar=raspado&categoria=3");

        // Reconstrucción del filtro desde la ruta (lo que hace el modelo en cada GET).
        var reconstruido = new TecnicasFiltroViewModel
        {
            Buscar = "raspado",
            Categoria = 3
        };

        reconstruido.ToRutaListado().Should().Be(desdeListado);
        reconstruido.ToSearchFilter().SearchTerm.Should().Be("raspado");
    }
}
