using FluentAssertions;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Web.Services.Recorrido;
using MiCaminoBJJ.Web.ViewModels.Recorrido;

namespace MiCaminoBJJ.Web.Tests.ViewModels;

/// <summary>
/// PASO 1.2 — presentación de la técnica relacionada en la ficha del recorrido.
///
/// Tras la importación del PDF la técnica relacionada puede llegar OCULTA
/// (IsActive = 0): se resuelve (nombre y cinturón reales) pero NO se enlaza a
/// su ficha pública, que respondería 404. Cubre las3 ramas del bloque
/// "Técnica en la biblioteca" de la vista Ficha.
/// </summary>
public class FichaDetalleViewModelTests
{
    private static FichaBlanco FichaConRelacionada =>
        CinturonBlancoCatalogo.Fichas.First(f => f.TecnicaRelacionada is not null);

    private static ModuloBlanco ModuloDe(FichaBlanco ficha) =>
        CinturonBlancoCatalogo.Modulos.First(m => m.Id == ficha.ModuloId);

    private static Technique Tecnica(int id, string nombre, bool activa, string cinturon) => new()
    {
        Id = id,
        Name = nombre,
        Description = "d",
        Objective = "o",
        IsActive = activa,
        Belt = new Belt { Id = 1, Name = cinturon, DisplayOrder = 1 }
    };

    [Fact]
    public void TecnicaRelacionadaActiva_EnlazaASuFichaPublica()
    {
        var ficha = FichaConRelacionada;
        var tecnica = Tecnica(42, "Nombre actual en la biblioteca", activa: true, "Violeta");

        var vm = FichaDetalleViewModel.Desde(
            ficha, ModuloDe(ficha), null, null, tecnica, bibliotecaNoDisponible: false);

        vm.TecnicaRelacionadaNombre.Should().Be("Nombre actual en la biblioteca");
        vm.TecnicaRelacionadaUrl.Should().Be("/Tecnicas/Details/42");
        vm.TecnicaRelacionadaCinturon.Should().Be("Violeta");
        vm.TecnicaRelacionadaOculta.Should().BeFalse();
        vm.BibliotecaNoDisponible.Should().BeFalse();
    }

    [Fact]
    public void TecnicaRelacionadaOculta_MuestraNombreYCinturon_SinEnlacePublico()
    {
        var ficha = FichaConRelacionada;
        var tecnica = Tecnica(43, "Oculta tras la importacion", activa: false, "Blanco");

        var vm = FichaDetalleViewModel.Desde(
            ficha, ModuloDe(ficha), null, null, tecnica, bibliotecaNoDisponible: false);

        // Se resuelve: nombre y cinturón reales visibles…
        vm.TecnicaRelacionadaNombre.Should().Be("Oculta tras la importacion");
        vm.TecnicaRelacionadaCinturon.Should().Be("Blanco");
        vm.TecnicaRelacionadaOculta.Should().BeTrue();

        // …pero SIN enlace: la ficha pública de una técnica oculta da 404.
        vm.TecnicaRelacionadaUrl.Should().BeNull();
        vm.BibliotecaNoDisponible.Should().BeFalse("SÍ se resolvió; solo está oculta");
    }

    [Fact]
    public void SinTecnicaRelacionada_NoMarcaOculta_NiDisponible()
    {
        var ficha = CinturonBlancoCatalogo.Fichas.First(f => f.TecnicaRelacionada is null);

        var vm = FichaDetalleViewModel.Desde(
            ficha, ModuloDe(ficha), null, null, tecnicaRelacionada: null,
            bibliotecaNoDisponible: false);

        vm.TecnicaRelacionadaNombre.Should().BeNull();
        vm.TecnicaRelacionadaUrl.Should().BeNull();
        vm.TecnicaRelacionadaOculta.Should().BeFalse();
        vm.BibliotecaNoDisponible.Should().BeFalse("sin referencia declarada no hay falla de biblioteca");
    }

    [Fact]
    public async Task CatalogoDelRecorrido_ConservaCincoModulos_YCuatroVideos()
    {
        // PASO 1.3 — los 5 módulos y los 4 videos complementarios no cambian
        // con la importación (el recorrido es contenido en código).
        CinturonBlancoCatalogo.Modulos.Should().HaveCount(5);

        var videos = CinturonBlancoCatalogo.Fichas
            .Where(f => f.Video is not null)
            .Select(f => f.Video!.UrlOriginal)
            .ToList();

        videos.Should().HaveCount(4, "el recorrido conserva sus 4 videos complementarios");
        videos.Should().OnlyContain(u => u.Contains("youtu", StringComparison.OrdinalIgnoreCase));

        await Task.CompletedTask;
    }
}
