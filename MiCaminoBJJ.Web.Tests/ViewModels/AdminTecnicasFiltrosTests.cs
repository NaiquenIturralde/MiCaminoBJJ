using FluentAssertions;
using MiCaminoBJJ.Web.ViewModels;
using MiCaminoBJJ.Web.ViewModels.Admin;

namespace MiCaminoBJJ.Web.Tests.ViewModels;

/// <summary>
/// Filtros del listado administrativo de técnicas (nombre/sinónimo, cinturón
/// y estado): conservación de los tres criterios entre pantallas y generación
/// del query string que los reenvía al volver al listado.
/// </summary>
public class AdminTecnicasFiltrosTests
{
    // ── Query string ──────────────────────────────────────────────────────

    [Fact]
    public void QueryString_SinFiltros_EsVacio_YLaRutaEsElListadoLimpio()
    {
        AdminTecnicasFiltros.QueryString(null, null, null).Should().BeEmpty();
        AdminTecnicasFiltros.RutaListado(null, null, null).Should().Be("/AdminTecnicas");
    }

    [Fact]
    public void QueryString_CadaFiltroPorSeparado_GeneraSoloSuParametro()
    {
        AdminTecnicasFiltros.QueryString("kimura", null, null)
            .Should().Be("?buscar=kimura");
        AdminTecnicasFiltros.QueryString(null, false, null)
            .Should().Be("?estado=false");
        AdminTecnicasFiltros.QueryString(null, null, 3)
            .Should().Be("?cinturon=3");
    }

    [Fact]
    public void QueryString_CombinaLosTresFiltros_YCodificaLaBusqueda()
    {
        AdminTecnicasFiltros.QueryString("mata león", true, 3)
            .Should().Be("?buscar=mata%20le%C3%B3n&estado=true&cinturon=3");
    }

    [Fact]
    public void QueryString_IgnoraBusquedaEnBlanco()
    {
        AdminTecnicasFiltros.QueryString("   ", null, 2)
            .Should().Be("?cinturon=2", "una búsqueda vacía no es un filtro");
    }

    // ── ViewModel del listado ─────────────────────────────────────────────

    [Fact]
    public void TieneFiltros_CualquieraDeLosTresPorSolo_ActivaElFiltro()
    {
        new AdminTecnicasIndexViewModel().TieneFiltros.Should().BeFalse();

        new AdminTecnicasIndexViewModel { Buscar = "kimura" }.TieneFiltros.Should().BeTrue();
        new AdminTecnicasIndexViewModel { Estado = false }.TieneFiltros.Should().BeTrue();
        new AdminTecnicasIndexViewModel { Cinturon = 3 }.TieneFiltros.Should().BeTrue();

        new AdminTecnicasIndexViewModel { Buscar = "   " }.TieneFiltros
            .Should().BeFalse("una búsqueda en blanco no cuenta como filtro");
    }

    [Fact]
    public void RutaListado_Y_RutaEdicion_ConservanLosTresFiltros()
    {
        var model = new AdminTecnicasIndexViewModel
        {
            Buscar = "kimura",
            Estado = false,
            Cinturon = 3
        };

        model.QueryString.Should().Be("?buscar=kimura&estado=false&cinturon=3");
        model.RutaListado.Should().Be("/AdminTecnicas?buscar=kimura&estado=false&cinturon=3");
        model.RutaEdicion(12).Should().Be("/AdminTecnicas/Edit/12?buscar=kimura&estado=false&cinturon=3");
    }

    [Fact]
    public void RutaEdicion_SinFiltros_EsLaRutaLimpia()
    {
        new AdminTecnicasIndexViewModel().RutaEdicion(12)
            .Should().Be("/AdminTecnicas/Edit/12");
    }

    [Fact]
    public void EstadoTexto_TraduceElNullableAValueDeCampoOculto()
    {
        new AdminTecnicasIndexViewModel().EstadoTexto.Should().BeEmpty();
        new AdminTecnicasIndexViewModel { Estado = true }.EstadoTexto.Should().Be("true");
        new AdminTecnicasIndexViewModel { Estado = false }.EstadoTexto.Should().Be("false");
    }

    [Fact]
    public void NombreCinturon_UsaElCatalogo_YEsNullSinFiltro()
    {
        var model = new AdminTecnicasIndexViewModel
        {
            Cinturon = 3,
            Cinturones = new[]
            {
                new FiltroOpcionViewModel { Id = 1, Nombre = "Blanco" },
                new FiltroOpcionViewModel { Id = 3, Nombre = "Violeta" }
            }
        };

        model.NombreCinturon.Should().Be("Violeta");
        new AdminTecnicasIndexViewModel().NombreCinturon.Should().BeNull();
    }

    // ── ViewModel del formulario (Crear/Editar) ───────────────────────────

    [Fact]
    public void Formulario_SinFiltros_NoArrastraNada()
    {
        var form = new AdminTecnicaFormViewModel();

        form.TieneFiltroListado.Should().BeFalse();
        form.QueryStringFiltro.Should().BeEmpty();
        form.RutaListado.Should().Be("/AdminTecnicas");
        form.FiltroEstadoTexto.Should().BeEmpty();
    }

    [Fact]
    public void Formulario_ArrastraLosTresFiltros_HaciaElListado()
    {
        var form = new AdminTecnicaFormViewModel
        {
            FiltroBuscar = "kimura",
            FiltroEstado = false,
            FiltroCinturon = 3
        };

        form.TieneFiltroListado.Should().BeTrue();
        form.FiltroEstadoTexto.Should().Be("false");
        form.RutaListado.Should().Be("/AdminTecnicas?buscar=kimura&estado=false&cinturon=3");
    }

    [Fact]
    public void Formulario_ArrastraSoloElCinturon()
    {
        var form = new AdminTecnicaFormViewModel { FiltroCinturon = 3 };

        form.TieneFiltroListado.Should().BeTrue();
        form.RutaListado.Should().Be("/AdminTecnicas?cinturon=3");
    }
}
