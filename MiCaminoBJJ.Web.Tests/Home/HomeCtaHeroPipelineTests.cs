using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MiCaminoBJJ.Web.Tests.Pipeline;

namespace MiCaminoBJJ.Web.Tests.Home;

/// <summary>
/// Optimización del CTA principal de la Home (hero): guards de regresión
/// sobre el HTML real emitido por la app, con la base AISLADA del pipeline.
///
/// Cubre las dos ramas del CTA:
/// - visitante → "¡Registrarme ahora!" hacia /Cuenta/Registro;
/// - práctico autenticado → "Ir a mi guía" hacia /GuiaPracticante.
/// Nunca escribe en la base principal.
/// </summary>
[Collection("SqlServerLocalDbPipeline")]
public class HomeCtaHeroPipelineTests : IClassFixture<SeguridadPipelineTests.Factory>
{
    private readonly SeguridadPipelineTests.Factory _factory;

    public HomeCtaHeroPipelineTests(SeguridadPipelineTests.Factory factory) => _factory = factory;

    private HttpClient CrearCliente() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> TokenDe(HttpClient cliente, string url)
    {
        var html = await cliente.GetStringAsync(url);
        var match = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"");
        match.Success.Should().BeTrue($"la página {url} debe emitir un token antiforgery");
        return match.Groups[1].Value;
    }

    /// <summary>Registro + login reales (cookies de Identity) sobre la base aislada.</summary>
    private static async Task RegistrarYLoginAsync(HttpClient cliente, string email, string password)
    {
        var tokenRegistro = await TokenDe(cliente, "/Cuenta/Registro");
        var registro = await cliente.PostAsync("/Cuenta/Registro", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Nombre"] = "Prueba CTA Hero",
                ["Email"] = email,
                ["Cinturon"] = "Blanco",
                ["Password"] = password,
                ["ConfirmarPassword"] = password,
                ["__RequestVerificationToken"] = tokenRegistro
            }));
        ((int)registro.StatusCode).Should().Be(302, "el registro válido redirige al login");

        var tokenLogin = await TokenDe(cliente, "/Cuenta/Login");
        var login = await cliente.PostAsync("/Cuenta/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password,
                ["__RequestVerificationToken"] = tokenLogin
            }));
        ((int)login.StatusCode).Should().Be(302, "el login válido redirige");
    }

    /// <summary>
    /// HTML decodificado y colapsado, acotado al &lt;section class="mccj-hero"&gt;
    /// para que las afirmaciones NO dependan de los enlaces del navbar.
    /// </summary>
    private static async Task<string> HeroDeAsync(HttpClient cliente)
    {
        var html = WebUtility.HtmlDecode(await cliente.GetStringAsync("/"));
        var match = Regex.Match(html, "<section class=\"mccj-hero\"[\\s\\S]*?</section>", RegexOptions.IgnoreCase);
        match.Success.Should().BeTrue("la Home debe emitir la sección del hero");
        return Regex.Replace(match.Value, @"\s+", " ");
    }

    private static string EmailUnico() => $"prueba-cta-{Guid.NewGuid():N}@example.com";

    // ── Visitante ────────────────────────────────────────────────────────

    [Fact]
    public async Task Visitante_MuestraCtaDeRegistro_YConservaExplorarTecnicas()
    {
        using var cliente = CrearCliente();
        var hero = await HeroDeAsync(cliente);

        hero.Should().Contain("Registrarme ahora!", "el CTA principal es el de registro");
        hero.Should().Contain("data-lucide=\"user-plus\"", "el CTA usa el ícono Lucide user-plus");
        hero.Should().Contain("href=\"/Cuenta/Registro\"", "ruta real de registro generada por Razor");
        hero.Should().Contain("mccj-cta", "el CTA lleva la clase de prioridad visual");

        hero.Should().Contain("Explorar técnicas", "el botón secundario se conserva");
        hero.Should().Contain("href=\"/Tecnicas\"", "la ruta actual de la biblioteca no cambia");

        // Orden visual: el CTA principal aparece antes que el secundario.
        hero.IndexOf("Registrarme ahora!", StringComparison.Ordinal)
            .Should().BeLessThan(hero.IndexOf("Explorar técnicas", StringComparison.Ordinal),
                "el CTA de registro va primero en la fila");

        hero.Should().NotContain("Ver funcionalidades", "botón eliminado del hero");
        hero.Should().NotContain("Cómo te acompaña", "botón eliminado del hero");
        hero.Should().NotContain("Ir a mi guía", "la variante de guía es solo para autenticados");
    }

    // ── Práctico autenticado ─────────────────────────────────────────────

    [Fact]
    public async Task Autenticado_MuestraGuiaEnLugarDelRegistro()
    {
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, EmailUnico(), "Passw0rd!cta2026");

        var hero = await HeroDeAsync(cliente);

        hero.Should().NotContain("Registrarme ahora!", "sin CTA de registro para quien ya inició sesión");
        hero.Should().NotContain("href=\"/Cuenta/Registro\"", "sin enlace de registro en el hero autenticado");

        hero.Should().Contain("Ir a mi guía", "la alternativa autenticada es la guía");
        hero.Should().Contain("data-lucide=\"compass\"", "ícono Lucide del CTA de guía");
        hero.Should().Contain("href=\"/GuiaPracticante\"", "la guía se enlaza por su ruta real");
        hero.Should().Contain("mccj-cta", "el CTA de guía conserva la prioridad visual");

        hero.Should().Contain("Explorar técnicas", "el botón secundario sigue presente");

        // Navegación real a la guía desde la sesión autenticada.
        var guia = await cliente.GetAsync("/GuiaPracticante");
        ((int)guia.StatusCode).Should().Be(200, "la ruta de la guía es accesible para el práctico logueado");
    }

    // ── Cards de funcionalidades (auditoría de cierre TFI, Etapa 1) ──────

    /// <summary>
    /// "Disponible" SOLO para lo implementado hoy en Web; el resto se
    /// comunica como "Próximamente" y ninguna tarjeta enlaza.
    /// </summary>
    [Fact]
    public async Task Funcionalidades_DisponibleSoloParaLoExistente_SinEnlaces()
    {
        using var cliente = CrearCliente();
        var html = WebUtility.HtmlDecode(await cliente.GetStringAsync("/"));

        var seccion = Regex.Match(html, "id=\"funcionalidades\"[\\s\\S]*?</section>", RegexOptions.IgnoreCase);
        seccion.Success.Should().BeTrue("la Home debe emitir la sección de funcionalidades");

        var tarjetas = Regex.Matches(seccion.Value, "<article[\\s\\S]*?</article>");
        tarjetas.Count.Should().Be(6, "seis tarjetas de funcionalidad");

        string[] disponibles = { "Biblioteca de técnicas", "Búsqueda rápida", "Seguimiento personalizado" };
        string[] proximamente = { "Registro de entrenamientos", "Seguimiento de progreso", "Técnicas guardadas" };

        foreach (Match tarjeta in tarjetas)
        {
            var badge = Regex.Match(tarjeta.Value, "mccj-badge\">([^<]+)<");
            badge.Success.Should().BeTrue("cada tarjeta de funcionalidad lleva badge");

            var titulo = Regex.Match(tarjeta.Value, "<h3>([^<]+)</h3>");
            titulo.Success.Should().BeTrue("cada tarjeta de funcionalidad lleva título");
            var nombre = Regex.Replace(titulo.Groups[1].Value, @"\s+", " ").Trim();

            if (disponibles.Contains(nombre))
                badge.Groups[1].Value.Should().Be("Disponible", "{0} existe en Web", nombre);
            else if (proximamente.Contains(nombre))
                badge.Groups[1].Value.Should().Be("Próximamente", "{0} todavía no existe en Web", nombre);
            else
                throw new InvalidOperationException(
                    $"Tarjeta inesperada en la Home: '{nombre}'. Definir su estado real antes de presentar.");

            tarjeta.Value.Should().NotContain("<a ",
                "la tarjeta {0} no debe enlazar a una funcionalidad inexistente", nombre);
        }
    }

    /// <summary>
    /// Ningún enlace interno visible de la Home (sin extensión de archivo)
    /// puede apuntar a una ruta que no exista en Web; el recorrido del
    /// cinturón blanco se alcanza desde la biblioteca (Etapa UI/UX: ya no
    /// es un ítem fijo del navbar).
    /// </summary>
    [Fact]
    public async Task Home_NoEnlazaARutasInexistentes_YAccedeAlRecorrido()
    {
        using var cliente = CrearCliente();
        var html = await cliente.GetStringAsync("/");

        var rutasPermitidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/", "/Home/Privacy", "/Cuenta/Login", "/Cuenta/Registro",
            "/Tecnicas", "/GuiaPracticante", "/recorrido/cinturon-blanco"
        };

        var internos = Regex.Matches(html, "href=\"(/[^\"]*)\"")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .Where(h => !h.Contains('.'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        internos.Should().NotBeEmpty("la Home debe emitir enlaces internos reales");
        internos.Should().BeSubsetOf(rutasPermitidas,
            "ningún enlace visible puede apuntar a una funcionalidad que no existe en Web");

        // El recorrido vive en la experiencia de biblioteca (contenido contextual).
        var biblioteca = await cliente.GetStringAsync("/Tecnicas");
        biblioteca.Should().Contain("/recorrido/cinturon-blanco",
            "el recorrido del cinturón blanco se alcanza desde la biblioteca");
    }
}
