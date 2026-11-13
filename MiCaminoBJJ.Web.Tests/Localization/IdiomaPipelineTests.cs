using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiCaminoBJJ.Web.Localization;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Tests.Pipeline;

namespace MiCaminoBJJ.Web.Tests.Localization;

/// <summary>
/// Validación END-TO-END del idioma de la aplicación sobre el pipeline HTTP
/// REAL (WebApplicationFactory: cookies, antiforgery, redirects y vistas).
///
/// Reproduce exactamente el recorrido del TFI:
/// 1. Registrarse e iniciar sesión (cookie real de Identity).
/// 2. Abrir «Configuración» y comprobar que ofrece los tres idiomas.
/// 3. Elegir un idioma y guardarlo (POST con antiforgery).
/// 4. Navegar: la navegación, botones y textos salen en el idioma elegido.
/// 5. RECARGAR: la preferencia sigue vigente.
/// 6. CERRAR SESIÓN y volver a entrar: la preferencia PERSISTE.
/// 7. Cambiar a otro idioma: el cambio se refleja de inmediato.
///
/// Base AISLADA y única por ejecución: nunca escribe en la base principal.
/// </summary>
[Collection("SqlServerLocalDbPipeline")]
public sealed class IdiomaPipelineTests : IClassFixture<SeguridadPipelineTests.Factory>
{
    private const string Password = "Abcdef1!";

    private readonly SeguridadPipelineTests.Factory _factory;

    public IdiomaPipelineTests(SeguridadPipelineTests.Factory factory) => _factory = factory;

    private HttpClient CrearCliente() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>
    /// Decodifica las entidades HTML del documento: Razor codifica los acentos
    /// (Espa&#xF1;ol), así que las comparaciones de texto deben hacerse sobre
    /// el documento decodificado.
    /// </summary>
    private static string Decodificar(string html) =>
        System.Net.WebUtility.HtmlDecode(html);

    private static async Task<string> TokenDe(HttpClient cliente, string url)
    {
        var html = await cliente.GetStringAsync(url);
        var match = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"");
        match.Success.Should().BeTrue($"la página {url} debe emitir un token antiforgery");
        return match.Groups[1].Value;
    }

    private async Task<(HttpClient Cliente, string Email)> PracticanteAsync()
    {
        var cliente = CrearCliente();
        var email = $"idioma-{Guid.NewGuid():N}@example.com";

        var tokenRegistro = await TokenDe(cliente, "/Cuenta/Registro");
        var registro = await cliente.PostAsync("/Cuenta/Registro", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Nombre"] = "Prueba Idioma",
                ["Email"] = email,
                ["Cinturon"] = "Blanco",
                ["Password"] = Password,
                ["ConfirmarPassword"] = Password,
                ["__RequestVerificationToken"] = tokenRegistro
            }));
        ((int)registro.StatusCode).Should().Be(302, "el registro válido redirige al login");

        var tokenLogin = await TokenDe(cliente, "/Cuenta/Login");
        var login = await cliente.PostAsync("/Cuenta/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = Password,
                ["__RequestVerificationToken"] = tokenLogin
            }));
        ((int)login.StatusCode).Should().Be(302, "el login válido redirige");

        return (cliente, email);
    }

    /// <summary>Guarda el idioma desde la pantalla de Configuración (POST real).</summary>
    private async Task GuardarIdiomaAsync(HttpClient cliente, string idioma)
    {
        var token = await TokenDe(cliente, "/Cuenta/Configuracion");
        var post = await cliente.PostAsync("/Cuenta/Configuracion", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Idioma"] = idioma,
                ["__RequestVerificationToken"] = token
            }));

        ((int)post.StatusCode).Should().Be(302, "guardar preferencias redirige (PRG)");
    }

    // ── 1) La pantalla de Configuración ofrece los tres idiomas ───────────

    [Fact]
    public async Task Configuracion_MuestraLosTresIdiomasYElBotonDeGuardar()
    {
        var (cliente, _) = await PracticanteAsync();

        var html = Decodificar(await cliente.GetStringAsync("/Cuenta/Configuracion"));

        html.Should().Contain("value=\"es\"");
        html.Should().Contain("value=\"en\"");
        html.Should().Contain("value=\"pt\"");

        // Los nombres de los idiomas se muestran traducidos al idioma actual.
        html.Should().Contain("Español");
        html.Should().Contain("English");
        html.Should().Contain("Português");

        // Botón de guardar y confirmación visible tras guardar.
        html.Should().Contain("Guardar preferencias");
    }

    [Fact]
    public async Task Configuracion_NoRepiteLosDatosDeLaCuenta()
    {
        var (cliente, email) = await PracticanteAsync();

        var html = Decodificar(await cliente.GetStringAsync("/Cuenta/Configuracion"));

        // La información duplicada (nombre, email, rol y cinturón) se elimina:
        // esos datos se administran desde «Mi perfil».
        html.Should().NotContain(email, "el email no se repite en Configuración");
        html.Should().NotContain("mccj-profile-data", "no se repite el bloque de datos del perfil");
    }

    // ── 2) El idioma elegido se aplica a la navegación y a las pantallas ───

    [Theory]
    [InlineData("en", "Home", "Technique library", "Practitioner's Guide", "Sign out")]
    [InlineData("pt", "Início", "Biblioteca de técnicas", "Guia do Praticante", "Sair")]
    public async Task ElIdiomaElegidoSeAplicaANavegacionYBotones(
        string idioma, string inicio, string biblioteca, string guia, string cerrarSesion)
    {
        var (cliente, _) = await PracticanteAsync();

        await GuardarIdiomaAsync(cliente, idioma);

        var html = Decodificar(await cliente.GetStringAsync("/"));

        html.Should().Contain(inicio);
        html.Should().Contain(biblioteca);
        html.Should().Contain(guia);
        html.Should().Contain(cerrarSesion);

        // El atributo lang del documento refleja el idioma activo.
        html.Should().MatchRegex(new Regex($"<html lang=\"{idioma}\"", RegexOptions.IgnoreCase));
    }

    [Fact]
    public async Task ElIdiomaElegidoSeAplicaAFormulariosYTitulos()
    {
        var (cliente, _) = await PracticanteAsync();

        await GuardarIdiomaAsync(cliente, "en");

        var html = Decodificar(await cliente.GetStringAsync("/"));

        html.Should().Contain("Everything you need to progress");
        html.Should().Contain("Practical tools to learn, review and follow your progress.");

        // La biblioteca también sale traducida (filtros y formulario).
        var biblioteca = Decodificar(await cliente.GetStringAsync("/Tecnicas"));
        biblioteca.Should().Contain("Filter by belt");
        biblioteca.Should().Contain("Search by name or synonym");
    }

    [Fact]
    public async Task ElIdiomaElegidoSeAplicaAValidaciones()
    {
        var (cliente, email) = await PracticanteAsync();

        await GuardarIdiomaAsync(cliente, "en");

        // Login con email inválido → el mensaje de validación sale en inglés.
        var token = await TokenDe(cliente, "/Cuenta/Login");
        var post = await cliente.PostAsync("/Cuenta/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Email"] = "no-es-un-email",
                ["Password"] = Password,
                ["__RequestVerificationToken"] = token
            }));

        var html = Decodificar(await post.Content.ReadAsStringAsync());
        html.Should().Contain("The email does not have a valid format.");
    }

    // ── 3) La preferencia PERSISTE al recargar ────────────────────────────

    [Fact]
    public async Task LaPreferenciaPersisteAlRecargar()
    {
        var (cliente, _) = await PracticanteAsync();

        await GuardarIdiomaAsync(cliente, "pt");

        // Recargas sucesivas: la preferencia sigue vigente.
        for (int i = 0; i < 3; i++)
        {
            var html = Decodificar(await cliente.GetStringAsync("/"));
            html.Should().Contain("Início", $"la recarga {i + 1} debe seguir en portugués");
        }
    }

    // ── 4) La preferencia PERSISTE al cerrar sesión y volver a entrar ─────

    [Fact]
    public async Task LaPreferenciaPersisteAlCerrarSesionYVolverAEntrar()
    {
        var (cliente, email) = await PracticanteAsync();

        await GuardarIdiomaAsync(cliente, "en");

        // Cerrar sesión (POST real con antiforgery).
        var tokenLogout = await TokenDe(cliente, "/");
        var logout = await cliente.PostAsync("/Cuenta/Logout", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["__RequestVerificationToken"] = tokenLogout }));
        ((int)logout.StatusCode).Should().Be(302);

        // Volver a entrar.
        var tokenLogin = await TokenDe(cliente, "/Cuenta/Login");
        var login = await cliente.PostAsync("/Cuenta/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = Password,
                ["__RequestVerificationToken"] = tokenLogin
            }));
        ((int)login.StatusCode).Should().Be(302);

        // La preferencia sigue vigente: vive en la base, no en la sesión.
        var html = Decodificar(await cliente.GetStringAsync("/"));
        html.Should().Contain("Home");
        html.Should().Contain("Sign out");
    }

    // ── 5) Cambiar de idioma se refleja de inmediato ───────────────────────

    [Fact]
    public async Task CambiarDeIdiomaSeReflejaDeInmediato()
    {
        var (cliente, _) = await PracticanteAsync();

        await GuardarIdiomaAsync(cliente, "en");
        Decodificar(await cliente.GetStringAsync("/")).Should().Contain("Home");

        await GuardarIdiomaAsync(cliente, "pt");
        Decodificar(await cliente.GetStringAsync("/")).Should().Contain("Início");

        await GuardarIdiomaAsync(cliente, "es");
        var html = Decodificar(await cliente.GetStringAsync("/"));
        html.Should().Contain("Inicio");
        html.Should().Contain("Cerrar sesión");
    }

    // ── 6) La preferencia queda en la base (no solo en la sesión) ──────────

    [Fact]
    public async Task LaPreferenciaQuedaPersistidaEnLaBase()
    {
        var (cliente, email) = await PracticanteAsync();

        await GuardarIdiomaAsync(cliente, "pt");

        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();

        var idioma = await db.UserSettings
            .AsNoTracking()
            .Where(a => a.User.Email == email)
            .Select(a => a.Language)
            .FirstOrDefaultAsync();

        idioma.Should().Be("pt", "el idioma queda guardado en UserSettings.Language");
    }

    // ── 7) La confirmación de guardado es VISIBLE ──────────────────────────

    [Fact]
    public async Task GuardarMuestraUnaConfirmacionVisible()
    {
        var (cliente, _) = await PracticanteAsync();

        await GuardarIdiomaAsync(cliente, "en");

        // Tras el redirect (PRG) la pantalla de Configuración muestra el aviso.
        var html = Decodificar(await cliente.GetStringAsync("/Cuenta/Configuracion"));
        html.Should().Contain("Preferences saved successfully.");
    }

    // ── 8) Un idioma NO soportado no se guarda ─────────────────────────────

    [Fact]
    public async Task UnIdiomaNoSoportadoNoSeGuarda()
    {
        var (cliente, email) = await PracticanteAsync();

        var token = await TokenDe(cliente, "/Cuenta/Configuracion");
        var post = await cliente.PostAsync("/Cuenta/Configuracion", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Idioma"] = "fr",
                ["__RequestVerificationToken"] = token
            }));

        ((int)post.StatusCode).Should().Be(200, "un idioma inválido vuelve al formulario");
        var html = Decodificar(await post.Content.ReadAsStringAsync());

        // El mensaje de validación sale en el idioma vigente (español por
        // defecto: el cliente de prueba no envía Accept-Language).
        html.Should().Contain("El idioma seleccionado no es válido.");

        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();

        var idioma = await db.UserSettings
            .AsNoTracking()
            .Where(a => a.User.Email == email)
            .Select(a => a.Language)
            .FirstOrDefaultAsync();

        idioma.Should().BeNull("no se guarda un idioma que la aplicación no soporta");
    }
}
