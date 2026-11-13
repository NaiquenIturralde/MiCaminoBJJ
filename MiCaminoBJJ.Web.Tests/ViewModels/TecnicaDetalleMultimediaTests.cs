using FluentAssertions;
using System.Text.RegularExpressions;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Web.Controllers;
using MiCaminoBJJ.Web.Services;
using MiCaminoBJJ.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace MiCaminoBJJ.Web.Tests.ViewModels;

/// <summary>
/// Reproductor integrado de la card "Multimedia" (biblioteca de técnicas).
///
/// Todo con datos SIMULADOS en memoria: no se escribe ni se consulta SQL
/// Server, no hay migraciones ni seeders. Cubre:
/// - mapeo URL de YouTube (watch / youtu.be / shorts / embed) → embed + miniatura;
/// - enlaces externos no-YouTube que quedan como lista simple;
/// - filtros de la vista (borrado lógico, tipo de fuente, URL vacía);
/// - técnica sin multimedia (mensaje de video pendiente de la vista);
/// - entrega del ViewModel por el controlador Details;
/// - guards de fuente: markup del reproductor e Include de EF.
/// </summary>
public class TecnicaDetalleMultimediaTests
{
    // ── Datos simulados ────────────────────────────────────────────────────

    private static MediaAsset Externo(string url, string? titulo = "Video demostrativo") => new()
    {
        Id = 1,
        SourceType = MediaSourceType.ExternalLink,
        ExternalUrl = url,
        Title = titulo,
        IsDeleted = false
    };

    private static Technique TecnicaCon(params MediaAsset[] assets)
    {
        var tecnica = new Technique
        {
            Id = 1,
            Name = "Kimura desde control lateral",
            Description = "Control lateral con ataque de brazo.",
            Objective = "Sumisión desde control lateral.",
            IsActive = true
        };

        var orden = 0;
        foreach (var asset in assets)
        {
            tecnica.OfficialMedia.Add(new TechniqueMedia
            {
                Id = ++orden,
                TechniqueId = tecnica.Id,
                Technique = tecnica,
                MediaAssetId = asset.Id,
                MediaAsset = asset,
                DisplayOrder = orden
            });
        }

        return tecnica;
    }

    private static TecnicaDetalleViewModel Detalle(Technique tecnica) =>
        TecnicaDetalleViewModel.Desde(tecnica, "/Tecnicas", esFuenteDeDemostracion: false);

    // ── YouTube: watch, shorts, youtu.be y embed ───────────────────────────

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=ynSK1oYc_gg", "ynSK1oYc_gg")]
    [InlineData("https://youtu.be/eDUjKxV6wb0", "eDUjKxV6wb0")]
    [InlineData("https://www.youtube.com/shorts/ynSK1oYc_gg", "ynSK1oYc_gg")]
    [InlineData("https://www.youtube.com/embed/7A72vo39F3Q", "7A72vo39F3Q")]
    public void YouTube_CualquierVariante_GeneraReproductorIntegrado(string url, string videoId)
    {
        var vm = Detalle(TecnicaCon(Externo(url)));

        // La URL debe producir exactamente un video integrado.
        var recurso = vm.Videos.Should().ContainSingle().Subject;

        recurso.EsVideoIntegrado.Should().BeTrue();
        recurso.EmbedUrl.Should().Be($"https://www.youtube.com/embed/{videoId}");
        recurso.MiniaturaUrl.Should().Be($"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg");
        recurso.Url.Should().Be($"https://www.youtube.com/watch?v={videoId}",
            "el enlace 'Abrir en YouTube' debe ser canónico (válido también para Shorts)");
        vm.TieneVideoIntegrado.Should().BeTrue();
        vm.EnlacesExternos.Should().BeEmpty("un video integrado no es un enlace simple");
    }

    [Fact]
    public void Recurso_ExponeNombreYDescripcionDelVideo()
    {
        var asset = Externo("https://youtu.be/eDUjKxV6wb0", "Titulo del video");
        asset.Description = "Descripcion del recurso en la ficha publica";

        var vm = Detalle(TecnicaCon(asset));

        var recurso = vm.Recursos.Should().ContainSingle().Subject;
        recurso.Titulo.Should().Be("Titulo del video");
        recurso.Descripcion.Should().Be("Descripcion del recurso en la ficha publica",
            "el PASO 3 exige mostrar nombre y descripción del video en la vista previa");
    }

    [Fact]
    public void EnlaceNoYouTube_QuedaComoEnlaceSimple_SinReproductor()
    {
        var vm = Detalle(TecnicaCon(Externo("https://example.com/archivo.pdf", "Apuntes")));

        vm.Videos.Should().BeEmpty();
        vm.TieneVideoIntegrado.Should().BeFalse();
        var enlace = vm.EnlacesExternos.Should().ContainSingle().Subject;
        enlace.Url.Should().Be("https://example.com/archivo.pdf");
        enlace.EsVideoIntegrado.Should().BeFalse();
        vm.Recursos.Should().HaveCount(1, "el enlace externo sigue siendo un recurso visible");
    }

    // ── Filtros de la vista (borrado lógico, tipo de fuente, URL vacía) ────

    [Fact]
    public void RecursosBorrados_NoLleganALaVista()
    {
        var borrado = Externo("https://youtu.be/eDUjKxV6wb0");
        borrado.IsDeleted = true;

        var vm = Detalle(TecnicaCon(borrado));

        vm.Recursos.Should().BeEmpty();
        vm.TieneVideoIntegrado.Should().BeFalse();
    }

    [Fact]
    public void RecursosNoExternos_NoLleganALaVista()
    {
        var local = Externo("https://youtu.be/eDUjKxV6wb0");
        local.SourceType = MediaSourceType.LocalFile;

        var vm = Detalle(TecnicaCon(local));

        vm.Recursos.Should().BeEmpty("los archivos locales aún no tienen mecanismo de servido en la web");
    }

    [Fact]
    public void RecursosSinUrl_NoLleganALaVista()
    {
        var sinUrl = Externo("   ");

        var vm = Detalle(TecnicaCon(sinUrl));

        vm.Recursos.Should().BeEmpty();
    }

    // ── Técnica sin video: mensaje de pendiente de la vista ────────────────

    [Fact]
    public void TecnicaSinMultimedia_NoTieneVideos_YLaVistaMuestraElMensajePendiente()
    {
        var vm = Detalle(new Technique { Id = 2, Name = "Sin recursos", IsActive = true });

        vm.Recursos.Should().BeEmpty();
        vm.Videos.Should().BeEmpty();
        vm.TieneVideoIntegrado.Should().BeFalse();

        var vista = LeerArchivoWeb("Views", "Tecnicas", "Details.cshtml");
        vista.Should().Contain("Tecnicas.Detalle.SinRecursos",
            "se conserva el mensaje cuando no hay multimedia asociada");
    }

    // ── Controlador: el video llega a la vista sin consultar la base ───────

    [Fact]
    public async Task Details_ConVideoSimulado_EntregaElReproductorALaVista()
    {
        var tecnica = TecnicaCon(Externo("https://www.youtube.com/shorts/ynSK1oYc_gg"));
        var controlador = new TecnicasController(
            new StubConsulta(tecnica),
            NullLogger<TecnicasController>.Instance);

        var resultado = await controlador.Details(tecnica.Id, null, null, null, null);

        var view = resultado.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<TecnicaDetalleViewModel>().Subject;
        vm.Videos.Should().ContainSingle();
        vm.Videos[0].EmbedUrl.Should().Be("https://www.youtube.com/embed/ynSK1oYc_gg");
        vm.Videos[0].MiniaturaUrl.Should().NotBeNullOrEmpty();
    }

    // ── Guards de fuente (markup y EF) ─────────────────────────────────────

    [Fact]
    public void Vista_TieneElReproductorDiferidoDelRecorrido()
    {
        var vista = LeerArchivoWeb("Views", "Tecnicas", "Details.cshtml");

        vista.Should().Contain("data-mccjrc-video", "contenedor que detecta el JS compartido");
        vista.Should().Contain("data-embed=\"@video.EmbedUrl\"", "URL de embed entregada al navegador");
        vista.Should().Contain("data-mccjrc-play", "botón Play de la vista previa");
        vista.Should().Contain("recorrido-video.js", "reutiliza el reproductor del recorrido sin duplicar scripts");
        vista.Should().Contain("recorrido.css", "reutiliza los estilos del reproductor (prefijo mccjrc-)");
        vista.Should().Contain("data-mccj-tecnica-enlace", "enlace secundario 'Abrir en YouTube' (texto localizado)");

        var css = LeerArchivoWeb("wwwroot", "css", "recorrido.css");
        css.Should().Contain("aspect-ratio: 16 / 9", "la vista previa es responsive 16:9");
        css.Should().Contain(".mccjrc-video-iframe", "el iframe ocupa el marco con allowfullscreen");
    }

    // ── Rediseño: el video es el elemento principal de la ficha ───────────

    [Fact]
    public void Vista_ElVideoQuedaArribaYElContenidoDebajoEnElOrdenPedido()
    {
        var vista = LeerArchivoWeb("Views", "Tecnicas", "Details.cshtml");

        // Los rótulos están localizados (@L["..."]), así que se verifica el
        // ORDEN de las secciones por su CONCEPTO (la clave del recurso, que es
        // estable e independiente del idioma) y por su POSICIÓN en el HTML.
        // Se comparan índices de CARACTERES (todos sobre el mismo texto), no
        // un índice de lista con un índice de carácter.
        var indiceVideo = vista.IndexOf("mccj-tecnica-video", StringComparison.Ordinal);
        var indiceDescripcion = vista.IndexOf("@L[\"Tecnicas.Detalle.Descripcion\"]", StringComparison.Ordinal);
        var indiceObjetivo = vista.IndexOf("@L[\"Tecnicas.Detalle.Objetivo\"]", StringComparison.Ordinal);
        var indicePasos = vista.IndexOf("@L[\"Tecnicas.Detalle.Pasos\"]", StringComparison.Ordinal);
        var indiceConsejos = vista.IndexOf("@L[\"Tecnicas.Detalle.Consejos\"]", StringComparison.Ordinal);

        indiceVideo.Should().BeGreaterThan(0, "la ficha debe tener el bloque de video");
        indiceDescripcion.Should().BeGreaterThanOrEqualTo(0, "debe existir la sección de descripción");
        indiceObjetivo.Should().BeGreaterThan(indiceDescripcion, "el objetivo va después de la descripción");
        indicePasos.Should().BeGreaterThan(indiceObjetivo, "los pasos van después del objetivo");
        indiceConsejos.Should().BeGreaterThan(indicePasos, "los consejos van después de los pasos");
        indiceDescripcion.Should().BeGreaterThan(indiceVideo, "el contenido va debajo del video");
    }

    [Fact]
    public void Vista_ElVideoNoQuedaEnUnaColumnaLateral()
    {
        var vista = LeerArchivoWeb("Views", "Tecnicas", "Details.cshtml");

        vista.Should().NotContain("col-lg-4", "el reproductor ya no vive en una columna lateral de 4 columnas");
        vista.Should().NotContain("col-lg-8", "no hay una columna de contenido que achique el video");
        vista.Should().Contain("mccj-tecnica-video", "el video es un bloque propio de ancho completo");
        vista.Should().Contain("tecnica-detalle.css", "estilos propios de la ficha (cards oscuras con borde dorado)");
    }

    [Fact]
    public void Vista_SinVideo_MuestraEstadoVacioElegante()
    {
        var vista = LeerArchivoWeb("Views", "Tecnicas", "Details.cshtml");

        vista.Should().Contain("mccj-tecnica-sin-video", "estado vacío con estilo propio");
        vista.Should().Contain("mccj-tecnica-sin-video-icono", "estado vacío con icono propio");
        vista.Should().Contain("Tecnicas.Detalle.SinVideo", "mensaje claro para la técnica sin demostración");
    }

    [Fact]
    public void Vista_VariosVideos_GaleriaDeMiniaturasYUnSoloReproductor()
    {
        var vista = LeerArchivoWeb("Views", "Tecnicas", "Details.cshtml");

        vista.Should().Contain("data-mccj-tecnica-video", "contenedor de la sección de video");
        vista.Should().Contain("data-mccj-tecnica-select", "miniatura seleccionable de video adicional");
        vista.Should().Contain("data-mccj-tecnica-poster", "la miniatura principal se puede actualizar");
        vista.Should().Contain("data-mccj-tecnica-enlace", "el enlace 'Abrir en YouTube' sigue al video elegido");
        vista.Should().Contain("tecnica-video.js", "JS que permite elegir otro video sin salir de la ficha");
    }

    [Fact]
    public void Js_DelReproductor_SoloCargaElVideoActivoYEnviaElReferer()
    {
        var jsReproductor = LeerArchivoWeb("wwwroot", "js", "recorrido-video.js");
        var jsSelector = LeerArchivoWeb("wwwroot", "js", "tecnica-video.js");

        jsReproductor.Should().Contain("referrerpolicy",
            "el iframe debe enviar Referer: sin él YouTube responde con el Error 153");
        jsReproductor.Should().Contain("strict-origin-when-cross-origin", "política de referencia del marco");

        jsSelector.Should().Contain("removeChild", "al cambiar de video se descarta el iframe anterior");
        jsSelector.Should().Contain("data-mccjrc-play", "el nuevo video se reproduce solo al pulsar Play");
    }

    [Fact]
    public void Servicio_CargaOfficialMediaYMediaAsset()
    {
        var servicio = LeerArchivoWeb("Services", "TecnicasSqlServerConsultaService.cs");

        servicio.Should().Contain(".Include(t => t.OfficialMedia).ThenInclude(tm => tm.MediaAsset)",
            "sin este Include la card Multimedia siempre recibiría la colección vacía");
    }

    [Fact]
    public void MarcadorDeReproductor_NoSeDuplicaPorPagina()
    {
        var vista = LeerArchivoWeb("Views", "Tecnicas", "Details.cshtml");

        vista.Should().Contain("@section Scripts", "el JS se carga una sola vez, vía sección del layout");
        vista.Should().Contain("Model.TieneVideoIntegrado",
            "el script solo se inyecta cuando la ficha tiene video integrado");
    }

    // ── Infraestructura de pruebas ─────────────────────────────────────────

    private static string LeerArchivoWeb(params string[] partesRelativas)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var raizProyecto = Path.Combine(dir.FullName, "MiCaminoBJJ.Web");
            if (Directory.Exists(raizProyecto))
            {
                var destino = Path.Combine(new[] { raizProyecto }.Concat(partesRelativas).ToArray());
                if (File.Exists(destino))
                    return File.ReadAllText(destino);
            }
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "No se encontró " + string.Join("/", partesRelativas) + " desde " + AppContext.BaseDirectory);
    }

    /// <summary>Servicio de solo lectura en memoria: nunca toca la base.</summary>
    private sealed class StubConsulta : ITecnicasConsultaService
    {
        private readonly Technique _tecnica;

        public StubConsulta(Technique tecnica) => _tecnica = tecnica;

        public bool EsFuenteDeDemostracion => false;

        public Task<IReadOnlyList<Technique>> SearchAsync(TechniqueSearchFilter filter) =>
            Task.FromResult<IReadOnlyList<Technique>>(new List<Technique> { _tecnica });

        public Task<Technique?> GetByIdAsync(int id) =>
            Task.FromResult<Technique?>(_tecnica.Id == id ? _tecnica : null);

        public Task<Technique?> GetByNombreOAliasExactoAsync(string? nombre) =>
            Task.FromResult<Technique?>(_tecnica);

        public Task<IReadOnlyList<Belt>> GetBeltsAsync() =>
            Task.FromResult<IReadOnlyList<Belt>>(new List<Belt>());

        public Task<IReadOnlyList<TechniqueCategory>> GetCategoriesAsync() =>
            Task.FromResult<IReadOnlyList<TechniqueCategory>>(new List<TechniqueCategory>());

        public Task<IReadOnlyList<Position>> GetPositionsAsync() =>
            Task.FromResult<IReadOnlyList<Position>>(new List<Position>());
    }
}
