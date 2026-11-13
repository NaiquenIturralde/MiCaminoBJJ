using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MiCaminoBJJ.Web.Tests.Pipeline;

namespace MiCaminoBJJ.Web.Tests.Guia;

/// <summary>
/// Fase B — pruebas HTTP del Guía del Practicante sobre la aplicación real
/// (WebApplicationFactory) con la base AISLADA única del pipeline.
///
/// Seguridad de las pruebas:
/// - La fábrica fuerza `GuiaPracticante__ApiKey` vacío: NINGÚN test puede
///   disparar una llamada real pagada al proveedor de IA.
/// - Solo lectura/escritura sobre la base de pruebas del arrendamiento.
/// - Serializada con las demás pruebas del pipeline (misma colección).
/// </summary>
[Collection("SqlServerLocalDbPipeline")]
public class GuiaPracticantePipelineTests : IClassFixture<SeguridadPipelineTests.Factory>
{
    private readonly SeguridadPipelineTests.Factory _factory;

    public GuiaPracticantePipelineTests(SeguridadPipelineTests.Factory factory) => _factory = factory;

    private HttpClient CrearCliente() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> TokenDe(HttpClient cliente, string url)
    {
        var html = await cliente.GetStringAsync(url);
        var match = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"");
        match.Success.Should().BeTrue($"la página {url} debe emitir un token antiforgery");
        return match.Groups[1].Value;
    }

    private static async Task RegistrarYLoginAsync(HttpClient cliente, string email, string password)
    {
        var tokenRegistro = await TokenDe(cliente, "/Cuenta/Registro");
        var registro = await cliente.PostAsync("/Cuenta/Registro", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Nombre"] = "Prueba Guia",
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

    private static async Task<HttpResponseMessage> ConsultarAsync(
        HttpClient cliente, string token, object cuerpo, bool incluirToken = true)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, "/GuiaPracticante/Consultar")
        {
            Content = JsonContent.Create(cuerpo)
        };

        if (incluirToken)
            solicitud.Headers.Add("RequestVerificationToken", token);

        return await cliente.SendAsync(solicitud);
    }

    private static string EmailUnico() => $"prueba-guia-{Guid.NewGuid():N}@example.com";

    /// <summary>
    /// HTML listo para afirmaciones de contenido: decodifica entidades
    /// (Razor escapa acentos/¿ como &#xF3;) y colapsa los saltos de línea que
    /// el archivo fuente de la vista puede contener dentro de un texto.
    /// </summary>
    private static string Normalizar(string html) =>
        Regex.Replace(WebUtility.HtmlDecode(html), @"\s+", " ");

    // ── Autorización ──────────────────────────────────────────────────────

    [Fact]
    public async Task Anonimo_RedirigeAlLoginPublico()
    {
        using var cliente = CrearCliente();

        var respuesta = await cliente.GetAsync("/GuiaPracticante");

        ((int)respuesta.StatusCode).Should().Be(302,
            "el guía es exclusivo para sesiones de Identity autenticadas");
        respuesta.Headers.Location!.ToString().Should().Contain("/Cuenta/Login");
    }

    [Fact]
    public async Task Anonimo_ConsultarTambienRedirigeAlLogin()
    {
        using var cliente = CrearCliente();

        // Token VÁLIDO emitido por una página pública: así el único rechazo
        // posible es la autorización (no el antiforgery).
        var token = await TokenDe(cliente, "/Cuenta/Login");
        var respuesta = await ConsultarAsync(cliente, token, new { texto = "hola" });

        ((int)respuesta.StatusCode).Should().Be(302,
            "sin sesión no se procesa ninguna consulta");
        respuesta.Headers.Location!.ToString().Should().Contain("/Cuenta/Login");
    }

    [Fact]
    public async Task Practico_VeLaPantalla_ConSugerenciasFijas_YModoHonesto()
    {
        var email = EmailUnico();
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, email, "Abcdef1!");

        var html = Normalizar(await cliente.GetStringAsync("/GuiaPracticante"));

        html.Should().Contain("Guía del Practicante");
        html.Should().Contain("¿Qué es una guardia?");
        html.Should().Contain("Mostrame técnicas de cinturón blanco");
        html.Should().Contain("¿Cómo puedo mejorar mi aprendizaje?");
        html.Should().Contain("Guía basada en catálogo",
            "sin API key la interfaz debe identificar el modo real");
        html.Should().NotContain(email, "la pantalla no filtra datos privados");
    }

    [Fact]
    public async Task LaNavbar_PublicaIncluyeElAccesoALaGuia()
    {
        using var cliente = CrearCliente();

        var home = Normalizar(await cliente.GetStringAsync("/"));

        home.Should().Contain("Guía del Practicante",
            "el acceso al agente vive en la navegación principal");
    }

    // ── Validación de mensajes ────────────────────────────────────────────

    [Fact]
    public async Task MensajeVacio_EsRechazado()
    {
        var email = EmailUnico();
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, email, "Abcdef1!");
        var token = await TokenDe(cliente, "/GuiaPracticante");

        var respuesta = await ConsultarAsync(cliente, token, new { texto = "" });

        ((int)respuesta.StatusCode).Should().Be(400);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("esError").GetBoolean().Should().BeTrue();
        json.RootElement.GetProperty("texto").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task MensajeExcesivamenteLargo_EsRechazado()
    {
        var email = EmailUnico();
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, email, "Abcdef1!");
        var token = await TokenDe(cliente, "/GuiaPracticante");

        var respuesta = await ConsultarAsync(cliente, token,
            new { texto = new string('a', 5000) });

        ((int)respuesta.StatusCode).Should().Be(400,
            "el límite de longitud se valida en el SERVIDOR");
    }

    [Fact]
    public async Task ConsultaSinTokenAntiforgery_EsRechazada()
    {
        var email = EmailUnico();
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, email, "Abcdef1!");

        var respuesta = await ConsultarAsync(
            cliente, token: string.Empty, new { texto = "¿Qué es una guardia?" },
            incluirToken: false);

        ((int)respuesta.StatusCode).Should().Be(400, "el antiforgery es obligatorio en el POST");
    }

    // ── Respuestas (modo catálogo, sin IA real) ───────────────────────────

    [Fact]
    public async Task Consulta_DeCatalogo_RespondeConContextoDeLasTecnicas()
    {
        var email = EmailUnico();
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, email, "Abcdef1!");
        var token = await TokenDe(cliente, "/GuiaPracticante");

        var respuesta = await ConsultarAsync(cliente, token, new { texto = "¿Qué es una guardia?" });

        ((int)respuesta.StatusCode).Should().Be(200);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("modo").GetString().Should().Be("catalogo",
            "sin API key la respuesta NO puede presentarse como IA generativa");
        json.RootElement.GetProperty("texto").GetString()!
            .Should().ContainEquivalentOf("guardia");
        json.RootElement.GetProperty("esError").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task BusquedaPorNombreOAlias_EncuentraTecnicasRegistradas()
    {
        var email = EmailUnico();
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, email, "Abcdef1!");
        var token = await TokenDe(cliente, "/GuiaPracticante");

        var respuesta = await ConsultarAsync(cliente, token, new { texto = "kimura" });

        ((int)respuesta.StatusCode).Should().Be(200);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var texto = json.RootElement.GetProperty("texto").GetString()!;
        texto.Should().ContainEquivalentOf("kimura",
            "la respuesta se basa en técnicas EXISTENTES de la biblioteca");
        texto.Should().NotContain("no está registrada");
    }

    [Fact]
    public async Task TecnicaInexistente_NuncaSeAfirmaComoRegistrada()
    {
        var email = EmailUnico();
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, email, "Abcdef1!");
        var token = await TokenDe(cliente, "/GuiaPracticante");

        var respuesta = await ConsultarAsync(cliente, token,
            new { texto = "Explicame la técnica Técnica Fantasma XYZ" });

        ((int)respuesta.StatusCode).Should().Be(200);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("texto").GetString()!
            .Should().Contain("no está registrada",
                "jamás se presenta como registrada una técnica que no existe");
    }

    // ── Privacidad ────────────────────────────────────────────────────────

    [Fact]
    public async Task LaRespuesta_NoExponeDatosPrivados()
    {
        var email = EmailUnico();
        const string password = "Abcdef1!";
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, email, password);
        var token = await TokenDe(cliente, "/GuiaPracticante");

        var respuesta = await ConsultarAsync(cliente, token,
            new { texto = "¿Cómo puedo mejorar mi aprendizaje?" });

        ((int)respuesta.StatusCode).Should().Be(200);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        cuerpo.Should().NotContain(email, "nunca se expone el correo del usuario");
        cuerpo.Should().NotContain("@example.com", "ningún correo del dominio de pruebas");
        cuerpo.Should().NotContain(password, "nunca se expone la contraseña");
    }

    [Fact]
    public async Task SinApiKey_LaAplicacion_NoDeclaraEstarUsandoIa()
    {
        var email = EmailUnico();
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, email, "Abcdef1!");
        var token = await TokenDe(cliente, "/GuiaPracticante");

        var respuesta = await ConsultarAsync(cliente, token, new { texto = "¿Qué es la montada?" });

        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        cuerpo.Should().Contain("\"modo\":\"catalogo\"",
            "el JSON identifica el modo real de la respuesta");
        cuerpo.Should().NotContain("\"modo\":\"ia\"");
    }
}
