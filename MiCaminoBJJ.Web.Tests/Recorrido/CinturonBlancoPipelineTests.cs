using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Tests.Pipeline;
using Xunit;

namespace MiCaminoBJJ.Web.Tests.Recorrido;

/// <summary>
/// Etapa 6A — pruebas de PIPELINE del recorrido cinturón blanco sobre el HTML
/// real emitido por la app, con la base AISLADA única del pipeline (nunca la
/// principal). Solo lectura: GET exclusivos y contados por SELECT al final.
/// </summary>
[Collection("SqlServerLocalDbPipeline")]
public class CinturonBlancoPipelineTests : IClassFixture<SeguridadPipelineTests.Factory>
{
    private static readonly string[] SlugsModulos =
    {
        "primeros-pasos",
        "fundamentos-defensivos",
        "supervivencia-en-montada",
        "supervivencia-en-control-lateral",
        "escapes-basicos"
    };

    private static readonly string[] Fichas =
    {
        "escape-de-montada-con-codos",
        "escape-de-control-lateral-recuperando-guardia",
        "recuperacion-de-guardia-desde-media-guardia",
        "defensa-ante-armbar"
    };

    // Emparejado POSICIONAL con Fichas[]: debe coincidir con el orden del
    // catálogo (recuperación de guardia → sCmv4Jc0qlU, defensa ante armbar →
    // yLGmZxSf2Vo), igual que documenta CinturonBlancoCatalogoTests.
    private static readonly string[] VideosPermitidos =
    {
        "ynSK1oYc_gg",
        "eDUjKxV6wb0",
        "sCmv4Jc0qlU",
        "yLGmZxSf2Vo"
    };

    private static readonly Regex UrlDeVideo = new(
        @"(?:i\.ytimg\.com/vi/|youtube\.com/embed/)([A-Za-z0-9_-]{11})",
        RegexOptions.Compiled);

    private readonly SeguridadPipelineTests.Factory _factory;

    public CinturonBlancoPipelineTests(SeguridadPipelineTests.Factory factory) => _factory = factory;

    private HttpClient CrearCliente() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>GET que debe responder 200; devuelve el HTML decodificado y colapsado.</summary>
    private static async Task<string> PaginaDeAsync(HttpClient cliente, string url)
    {
        var respuesta = await cliente.GetAsync(url);
        ((int)respuesta.StatusCode).Should().Be(200, $"GET {url} debe responder 200");
        return Decodificar(await respuesta.Content.ReadAsStringAsync());
    }

    /// <summary>HTML con entidades decodificadas y espacios colapsados (acentos y ¿ en Razor).</summary>
    private static string Decodificar(string html) =>
        Regex.Replace(WebUtility.HtmlDecode(html), @"\s+", " ");

    private static async Task<HttpResponseMessage> GetAsync(HttpClient cliente, string url) =>
        await cliente.GetAsync(url);

    /// <summary>Conteo por SELECT sobre la base AISLADA del pipeline (solo lectura).</summary>
    private async Task<(int Tecnicas, int Cinturones)> ContarAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WebTfDbContext>();

        var tecnicas = await db.Techniques.AsNoTracking().CountAsync();
        var cinturones = await db.Belts.AsNoTracking().CountAsync();
        return (tecnicas, cinturones);
    }

    // ── Índice ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Indice_Responde200_ConEncabezadoCompleto()
    {
        using var cliente = CrearCliente();
        var html = await PaginaDeAsync(cliente, "/recorrido/cinturon-blanco");

        html.Should().Contain("Cinturón Blanco");
        html.Should().Contain("Supervivencia y defensa básica");
        html.Should().Contain("Todo camino comienza con una base sólida");
        html.Should().Contain("Antes de atacar, aprendé a sobrevivir");
        html.Should().Contain("Por dónde arrancar");
        html.Should().Contain("Jiu-Jitsu University", "el marco pedagógico declarado debe mostrarse");
    }

    [Fact]
    public async Task Indice_MuestraLosCincoModulos_YLasCuatroFichas()
    {
        using var cliente = CrearCliente();
        var html = await PaginaDeAsync(cliente, "/recorrido/cinturon-blanco");

        foreach (var slug in SlugsModulos)
            html.Should().Contain($"/recorrido/cinturon-blanco/modulo/{slug}");

        foreach (var slug in Fichas)
            html.Should().Contain($"/recorrido/cinturon-blanco/ficha/{slug}");

        html.Should().Contain("Video pendiente", "los módulos sin demostración deben marcarlo discreto");
        html.Should().Contain("Pendiente de revisión del instructor");
    }

    [Fact]
    public async Task Indice_ListaLasTecnicasBlancoDeLaBiblioteca()
    {
        using var cliente = CrearCliente();
        var html = await PaginaDeAsync(cliente, "/recorrido/cinturon-blanco");

        html.Should().Contain("Escape de montada");
        html.Should().Contain("Mata león");
        html.Should().Contain("Armbar desde guardia cerrada");
        html.Should().Contain("Raspado básico");
        html.Should().Contain("Posicionamiento de supervivencia con control lateral");
        html.Should().Contain("/Tecnicas/Details/", "cada técnica se enlaza a su ficha existente");
    }

    // ── Acceso contextual (Etapa UI/UX: fuera del navbar principal) ──────

    [Fact]
    public async Task Recorrido_AccesibleDesdeLaBiblioteca_YSeVuelveDesdeElSitio()
    {
        using var cliente = CrearCliente();

        // El navbar principal YA no fija el recorrido (decisión de la etapa).
        var home = await PaginaDeAsync(cliente, "/");
        home.Should().NotContain("Recorrido cinturón blanco",
            "el recorrido ya no es un ítem fijo del menú superior");

        // El acceso vive en la experiencia de biblioteca (contenido contextual).
        var biblioteca = await PaginaDeAsync(cliente, "/Tecnicas");
        biblioteca.Should().Contain("/recorrido/cinturon-blanco",
            "la biblioteca ofrece el acceso contextual al recorrido");

        var recorrido = await PaginaDeAsync(cliente, "/recorrido/cinturon-blanco");
        recorrido.Should().Contain("href=\"/Tecnicas\"", "ida y vuelta hacia la biblioteca");
        recorrido.Should().Contain("href=\"/\"", "y de vuelta al inicio del sitio");
    }

    // ── Módulos ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Modulos_LosCinco_Responde200_ConTituloYContenido()
    {
        using var cliente = CrearCliente();

        foreach (var slug in SlugsModulos)
        {
            var html = await PaginaDeAsync(cliente, $"/recorrido/cinturon-blanco/modulo/{slug}");
            html.Should().Contain("Módulo", $"el módulo {slug} debe indicar su posición en el recorrido");
            html.Should().Contain("/recorrido/cinturon-blanco", $"el módulo {slug} enlaza al índice");
        }
    }

    [Fact]
    public async Task Modulo_Supervivencia_DistingueSupervivenciaDeEscape()
    {
        using var cliente = CrearCliente();

        foreach (var slug in new[] { "supervivencia-en-montada", "supervivencia-en-control-lateral" })
        {
            var html = await PaginaDeAsync(cliente, $"/recorrido/cinturon-blanco/modulo/{slug}");
            html.Should().Contain("SUPERVIVENCIA", $"el módulo {slug} debe definir el concepto");
            html.Should().Contain("ESCAPE", $"el módulo {slug} debe definir el concepto");
            html.Should().Contain("Video pendiente", $"el módulo {slug} no tiene demostración confirmada");
        }
    }

    [Fact]
    public async Task Modulo_Desconocido_Devuelve404()
    {
        using var cliente = CrearCliente();
        var respuesta = await GetAsync(cliente, "/recorrido/cinturon-blanco/modulo/no-existe");

        ((int)respuesta.StatusCode).Should().Be(404);
    }

    // ── Fichas ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fichas_LasCuatro_Responde200_ConSeccionesCompletas()
    {
        using var cliente = CrearCliente();

        foreach (var slug in Fichas)
        {
            var html = await PaginaDeAsync(cliente, $"/recorrido/cinturon-blanco/ficha/{slug}");

            html.Should().Contain("Posición inicial", $"ficha {slug}");
            html.Should().Contain("Objetivo", $"ficha {slug}");
            html.Should().Contain("Paso a paso", $"ficha {slug}");
            html.Should().Contain("Principios técnicos", $"ficha {slug}");
            html.Should().Contain("Errores frecuentes", $"ficha {slug}");
            html.Should().Contain("Precauciones de seguridad", $"ficha {slug}");
            html.Should().Contain("Estado de revisión técnica", $"ficha {slug}");
            html.Should().Contain("Pendiente de revisión del instructor", $"ficha {slug}");
            html.Should().Contain("Video demostrativo", $"ficha {slug}");
        }
    }

    [Fact]
    public async Task Fichas_MuestranVideoConVistaPrevia_SinIframeNiAutoplay()
    {
        using var cliente = CrearCliente();

        for (var i = 0; i < Fichas.Length; i++)
        {
            var slug = Fichas[i];
            var html = await PaginaDeAsync(cliente, $"/recorrido/cinturon-blanco/ficha/{slug}");
            var id = VideosPermitidos[i];

            // Vista previa integrada (miniatura externa) + botón Play.
            html.Should().Contain("data-mccjrc-video", $"ficha {slug}");
            html.Should().Contain($"i.ytimg.com/vi/{id}/hqdefault.jpg", $"ficha {slug}");
            html.Should().Contain("data-mccjrc-play", $"ficha {slug}");
            html.Should().Contain("Reproducir", $"ficha {slug}");
            html.Should().Contain("loading=\"lazy\"", $"ficha {slug}");
            html.Should().Contain($"youtube.com/watch?v={id}", $"ficha {slug}: opción abrir en YouTube");

            // Sin reproducción automática: el iframe recién se arma en el navegador.
            html.Should().NotContain("<iframe", $"ficha {slug}");
            html.Should().NotContain("autoplay", $"ficha {slug}");
        }
    }

    [Fact]
    public async Task Ficha_DeEscapeDeMontada_EnlazaLaTecnicaExistente_DeCinturonBlanco()
    {
        using var cliente = CrearCliente();
        var html = await PaginaDeAsync(cliente, "/recorrido/cinturon-blanco/ficha/escape-de-montada-con-codos");

        html.Should().Contain("Escape de montada", "enlaza con la técnica ya cargada en la biblioteca");
        html.Should().Contain("Cinturón principal: Blanco");
        html.Should().Contain("/Tecnicas/Details/");
        html.Should().NotContain(
            "Material complementario",
            "su técnica relacionada ES de cinturón blanco: no es material complementario");
    }

    [Fact]
    public async Task Ficha_DeControlLateral_MarcaMaterialComplementario_SinCambiarSuCinturon()
    {
        using var cliente = CrearCliente();
        var html = await PaginaDeAsync(cliente, "/recorrido/cinturon-blanco/ficha/escape-de-control-lateral-recuperando-guardia");

        html.Should().Contain("Cinturón principal: Violeta", "conserva el cinturón real de la biblioteca");
        html.Should().Contain("Material complementario del recorrido blanco");
    }

    [Fact]
    public async Task Ficha_SinTecnicaRelacionada_MuestraCinturonSinAsignar()
    {
        using var cliente = CrearCliente();
        var html = await PaginaDeAsync(cliente, "/recorrido/cinturon-blanco/ficha/recuperacion-de-guardia-desde-media-guardia");

        html.Should().Contain("Cinturón principal: sin asignar");
        html.Should().NotContain("Cinturón principal: Blanco");
    }

    [Fact]
    public async Task Ficha_Desconocida_Devuelve404()
    {
        using var cliente = CrearCliente();
        var respuesta = await GetAsync(cliente, "/recorrido/cinturon-blanco/ficha/no-existe");

        ((int)respuesta.StatusCode).Should().Be(404);
    }

    // ── Videos: solo los cuatro confirmados ───────────────────────────────

    [Fact]
    public async Task Recorrido_SoloAparecenLosCuatroVideosConfirmados()
    {
        using var cliente = CrearCliente();

        var urls = new List<string> { "/recorrido/cinturon-blanco" };
        urls.AddRange(SlugsModulos.Select(s => $"/recorrido/cinturon-blanco/modulo/{s}"));
        urls.AddRange(Fichas.Select(s => $"/recorrido/cinturon-blanco/ficha/{s}"));

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var url in urls)
        {
            var html = await PaginaDeAsync(cliente, url);
            foreach (Match coincidencia in UrlDeVideo.Matches(html))
                ids.Add(coincidencia.Groups[1].Value);
        }

        ids.Should().BeEquivalentTo(
            VideosPermitidos,
            "el recorrido no puede inventar ni repetir videos fuera de los cuatro autorizados");
    }

    // ── Integridad de la biblioteca (base AISLADA) ────────────────────────

    [Fact]
    public async Task Biblioteca_NoSeDuplicaNiSeModifica_AlNavegarElRecorrido()
    {
        var antes = await ContarAsync();
        antes.Tecnicas.Should().Be(11, "el seeder idempotente carga 11 técnicas");
        antes.Cinturones.Should().Be(5, "5 cinturones: Blanco, Azul, Violeta, Marrón y Negro");

        using var cliente = CrearCliente();
        var urls = new List<string> { "/recorrido/cinturon-blanco" };
        urls.AddRange(SlugsModulos.Select(s => $"/recorrido/cinturon-blanco/modulo/{s}"));
        urls.AddRange(Fichas.Select(s => $"/recorrido/cinturon-blanco/ficha/{s}"));

        foreach (var url in urls)
            await PaginaDeAsync(cliente, url);

        var despues = await ContarAsync();
        despues.Should().Be(antes, "el recorrido es de solo lectura: no crea ni modifica entidades");
    }
}
