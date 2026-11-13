using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Tests.Pipeline;

namespace MiCaminoBJJ.Web.Tests.Cuenta;

/// <summary>
/// «Mi perfil» y «Ayuda» sobre el pipeline HTTP REAL (base AISLADA propia).
///
/// Cubre: la pantalla de perfil única (foto 120×120 con avatar de
/// iniciales, edición de nombre y cinturón, botón «Guardar cambios»,
/// ausencia de los botones de navegación antiguos), la carga validada de la
/// fotografía (tipo, tamaño y contenido real), el control de acceso de la
/// foto (solo su dueño la ve) y la pantalla de Ayuda con su enlace en el
/// menú del avatar.
///
/// Aislamiento: base única por ejecución (TestDatabaseLease) y almacenamiento
/// de fotos en una carpeta TEMPORAL (nada se escribe en el repositorio).
/// </summary>
[Collection("SqlServerLocalDbPipeline")]
public class PerfilPracticoTests : IClassFixture<SeguridadPipelineTests.Factory>, IDisposable
{
    private const string Password = "Abcdef1!";

    /// <summary>Variable de entorno que apunta el almacenamiento de fotos a una carpeta temporal.</summary>
    private const string VariableRutaFotos = "PerfilFotos__Ruta";

    /// <summary>PNG 1×1 válido (bytes reales, no texto).</summary>
    private static readonly byte[] PngValido = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private readonly SeguridadPipelineTests.Factory _factory;
    private readonly string _carpetaFotos;

    public PerfilPracticoTests(SeguridadPipelineTests.Factory factory)
    {
        _factory = factory;
        _carpetaFotos = Path.Combine(Path.GetTempPath(), "MiCaminoBJJ.Web.Tests", Guid.NewGuid().ToString("N"));

        // La app lee la ruta de almacenamiento de la configuración: se fija
        // ANTES de la primera petición (el host se construye de forma perezosa).
        Environment.SetEnvironmentVariable(VariableRutaFotos, _carpetaFotos);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(VariableRutaFotos, null);
        try
        {
            if (Directory.Exists(_carpetaFotos))
                Directory.Delete(_carpetaFotos, recursive: true);
        }
        catch (IOException)
        {
            // La limpieza del temporal nunca debe romper la suite.
        }
    }

    private HttpClient CrearCliente() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> TokenDe(HttpClient cliente, string url)
    {
        var html = await cliente.GetStringAsync(url);
        var match = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"");
        match.Success.Should().BeTrue($"la página {url} debe emitir un token antiforgery");
        return match.Groups[1].Value;
    }

    private async Task RegistrarYLoginAsync(HttpClient cliente, string nombre, string email)
    {
        var tokenRegistro = await TokenDe(cliente, "/Cuenta/Registro");
        var registro = await cliente.PostAsync("/Cuenta/Registro", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Nombre"] = nombre,
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
    }

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient cliente,
        string url,
        Dictionary<string, string> datos) =>
        await cliente.PostAsync(url, new FormUrlEncodedContent(datos));

    /// <summary>POST multipart (formulario con archivo) para la carga de la foto.</summary>
    private static async Task<HttpResponseMessage> PostConArchivoAsync(
        HttpClient cliente,
        string url,
        string token,
        Dictionary<string, string> campos,
        string nombreArchivo,
        byte[] contenido,
        string contentType)
    {
        using var contenidoFormulario = new MultipartFormDataContent();

        foreach (var campo in campos)
            contenidoFormulario.Add(new StringContent(campo.Value), campo.Key);

        contenidoFormulario.Add(new StringContent(token), "__RequestVerificationToken");

        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        contenidoFormulario.Add(archivo, "foto", nombreArchivo);

        return await cliente.PostAsync(url, contenidoFormulario);
    }

    private static string EmailUnico(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}@example.com";

    /// <summary>Perfil de dominio del practicante (verificación en la base AISLADA).</summary>
    private async Task<(string Nombre, string Cinturon, UserRole Rol)> PerfilEnBaseAsync(string email)
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        var perfil = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        return (perfil.Name, perfil.BeltRank, perfil.Role);
    }

    // ── 1. La pantalla de perfil ──────────────────────────────────────────

    [Fact]
    public async Task Perfil_MuestraFotoMedianaAvatarDeInicialesYGuardarCambios()
    {
        var email = EmailUnico("perfil-vista");
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, "Ana Practica", email);

        var respuesta = await cliente.GetAsync("/Cuenta/Perfil");
        ((int)respuesta.StatusCode).Should().Be(200);

        var html = await respuesta.Content.ReadAsStringAsync();

        // Foto mediana (120×120) con esquinas redondeadas.
        html.Should().Contain("mccj-profile-photo");
        html.Should().Contain("mccj-profile-photo-block");

        // Sin foto todavía: avatar de iniciales.
        html.Should().Contain("mccj-profile-photo__iniciales");
        html.Should().Contain("AP", "las iniciales del avatar de respaldo");

        // Nombre y cinturón editables; email, rol y registro visibles.
        html.Should().Contain("Guardar cambios");
        html.Should().Contain("name=\"Nombre\"");
        html.Should().Contain("name=\"Cinturon\"");
        html.Should().Contain(email);
        html.Should().Contain("Practicante");
        html.Should().Contain("Miembro desde");

        // Se eliminaron los botones de navegación antiguos de esta pantalla.
        html.Should().NotContain("Explorar biblioteca");
        html.Should().NotContain("Volver al inicio");
    }

    [Fact]
    public async Task Perfil_GuardarCambios_ActualizaNombreYCinturon_SinTocarElRol()
    {
        var email = EmailUnico("perfil-guardar");
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, "Nombre Anterior", email);

        var token = await TokenDe(cliente, "/Cuenta/Perfil");
        var respuesta = await PostFormAsync(cliente, "/Cuenta/Perfil", new Dictionary<string, string>
        {
            ["Nombre"] = "Nombre Actualizado",
            ["Cinturon"] = "Azul",
            ["__RequestVerificationToken"] = token
        });

        ((int)respuesta.StatusCode).Should().Be(302, "los cambios válidos redirigen al perfil");
        respuesta.Headers.Location!.ToString().Should().Contain("/Cuenta/Perfil");

        var (nombre, cinturon, rol) = await PerfilEnBaseAsync(email);
        nombre.Should().Be("Nombre Actualizado");
        cinturon.Should().Be("Azul");
        rol.Should().Be(UserRole.Practicante, "el cinturón es dato de perfil: el rol no cambia");

        // La pantalla muestra el dato nuevo y la confirmación de éxito.
        var html = WebUtility.HtmlDecode(await cliente.GetStringAsync("/Cuenta/Perfil"));
        html.Should().Contain("Nombre Actualizado");
        html.Should().Contain("Cambios guardados correctamente.");
    }

    [Fact]
    public async Task Perfil_RechazaCinturonFueraDelCatalogo_NoModificaElPerfil()
    {
        var email = EmailUnico("perfil-cinturon");
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, "Ana Practica", email);

        var token = await TokenDe(cliente, "/Cuenta/Perfil");
        var respuesta = await PostFormAsync(cliente, "/Cuenta/Perfil", new Dictionary<string, string>
        {
            ["Nombre"] = "Ana Practica",
            ["Cinturon"] = "Dorado",
            ["__RequestVerificationToken"] = token
        });

        ((int)respuesta.StatusCode).Should().Be(200, "un cinturón fuera del catálogo vuelve al formulario");
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        html.Should().Contain("cinturón de la lista");

        var (_, cinturon, _) = await PerfilEnBaseAsync(email);
        cinturon.Should().Be("Blanco", "el cinturón inválido no se persiste");
    }

    // ── 2. Fotografía: validación y persistencia ──────────────────────────

    [Fact]
    public async Task Perfil_SubeFotoValida_LaGuardaYLaSirveConControlDeAcceso()
    {
        var email = EmailUnico("perfil-foto");
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, "Ana Practica", email);

        var token = await TokenDe(cliente, "/Cuenta/Perfil");
        var respuesta = await PostConArchivoAsync(
            cliente,
            "/Cuenta/Perfil",
            token,
            new Dictionary<string, string> { ["Nombre"] = "Ana Practica", ["Cinturon"] = "Blanco" },
            "mi-foto.png",
            PngValido,
            "image/png");

        ((int)respuesta.StatusCode).Should().Be(302, "una foto válida se guarda");

        // La foto persiste y se ve después de recargar.
        var html = await cliente.GetStringAsync("/Cuenta/Perfil");
        html.Should().Contain("/Cuenta/FotoPerfil/");
        html.Should().Contain("mccj-profile-photo__img");
        html.Should().NotContain("mccj-profile-photo__iniciales", "ya hay foto: no se muestra el avatar");

        // Vínculo persistido en el modelo existente (UserProfileMedia).
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        var perfil = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        var vinculos = await db.UserProfileMedia
            .Where(pm => pm.UserId == perfil.Id && pm.IsProfilePicture)
            .Select(pm => new { pm.MediaAssetId, pm.MediaAsset.MimeType, pm.MediaAsset.LocalPath })
            .ToListAsync();

        vinculos.Should().HaveCount(1, "un único vínculo de foto de perfil");
        vinculos[0].MimeType.Should().Be("image/png");
        vinculos[0].LocalPath.Should().NotBeNullOrWhiteSpace();

        // La imagen se sirve con el tipo correcto.
        var imagen = await cliente.GetAsync($"/Cuenta/FotoPerfil/{vinculos[0].MediaAssetId}");
        ((int)imagen.StatusCode).Should().Be(200);
        imagen.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
    }

    [Fact]
    public async Task Perfil_LaFotoDeOtroPracticante_NoSeSirve()
    {
        var emailDueño = EmailUnico("perfil-foto-dueno");
        var emailAjeno = EmailUnico("perfil-foto-ajeno");

        using var clienteDueño = CrearCliente();
        await RegistrarYLoginAsync(clienteDueño, "Ana Practica", emailDueño);

        var token = await TokenDe(clienteDueño, "/Cuenta/Perfil");
        var respuesta = await PostConArchivoAsync(
            clienteDueño,
            "/Cuenta/Perfil",
            token,
            new Dictionary<string, string> { ["Nombre"] = "Ana Practica", ["Cinturon"] = "Blanco" },
            "mi-foto.png",
            PngValido,
            "image/png");
        ((int)respuesta.StatusCode).Should().Be(302);

        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        var perfil = await db.Users.AsNoTracking().SingleAsync(u => u.Email == emailDueño);
        var assetId = await db.UserProfileMedia
            .Where(pm => pm.UserId == perfil.Id && pm.IsProfilePicture)
            .Select(pm => pm.MediaAssetId)
            .FirstAsync();

        // Otro practicante autenticado NO puede ver esa foto.
        using var clienteAjeno = CrearCliente();
        await RegistrarYLoginAsync(clienteAjeno, "Bruno Practica", emailAjeno);

        var intento = await clienteAjeno.GetAsync($"/Cuenta/FotoPerfil/{assetId}");
        ((int)intento.StatusCode).Should().Be((int)HttpStatusCode.NotFound,
            "la foto de otro usuario no se sirve");
    }

    [Fact]
    public async Task Perfil_RechazaArchivoQueNoEsUnaImagen_NoGuardaNada()
    {
        var email = EmailUnico("perfil-foto-mala");
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, "Ana Practica", email);

        var token = await TokenDe(cliente, "/Cuenta/Perfil");
        var respuesta = await PostConArchivoAsync(
            cliente,
            "/Cuenta/Perfil",
            token,
            new Dictionary<string, string> { ["Nombre"] = "Ana Practica", ["Cinturon"] = "Blanco" },
            "no-es-imagen.png",
            Encoding.UTF8.GetBytes("esto no es una imagen"),
            "image/png");

        ((int)respuesta.StatusCode).Should().Be(200, "un archivo que no es imagen vuelve al formulario");
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        html.Should().Contain("El archivo no es una imagen válida.");

        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        var perfil = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
        (await db.UserProfileMedia.CountAsync(pm => pm.UserId == perfil.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Perfil_RechazaExtensionNoPermitida()
    {
        var email = EmailUnico("perfil-foto-extension");
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, "Ana Practica", email);

        var token = await TokenDe(cliente, "/Cuenta/Perfil");
        var respuesta = await PostConArchivoAsync(
            cliente,
            "/Cuenta/Perfil",
            token,
            new Dictionary<string, string> { ["Nombre"] = "Ana Practica", ["Cinturon"] = "Blanco" },
            "programa.exe",
            Encoding.UTF8.GetBytes("MZ executable"),
            "application/octet-stream");

        ((int)respuesta.StatusCode).Should().Be(200);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        html.Should().Contain("Solo se admiten imágenes JPG, PNG o WEBP.");
    }

    // ── 3. Ayuda ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Ayuda_EsPublicaYDescribeSoloLoDisponible()
    {
        using var cliente = CrearCliente();
        var respuesta = await cliente.GetAsync("/Ayuda");

        ((int)respuesta.StatusCode).Should().Be(200, "la ayuda se puede consultar sin iniciar sesión");
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());

        // Las nueve secciones pedidas (se comprueban por su ancla: son
        // estables aunque el texto se traduzca).
        foreach (var ancla in new[]
                 {
                     "que-es", "registro", "biblioteca", "cinturon",
                     "videos", "guia", "perfil", "faq", "roles"
                 })
        {
            html.Should().Contain($"id=\"{ancla}\"", $"la sección '{ancla}' del manual debe existir");
        }

        // Contenido obligatorio de cada bloque.
        html.Should().Contain("Practicante");
        html.Should().Contain("Administrador");
        html.Should().Contain("Guardar cambios", "el manual explica cómo guardar los cambios del perfil");
        html.Should().Contain("2 MB", "el manual documenta el límite de la foto de perfil");

        // La ayuda NO muestra claves de recursos sin traducir.
        html.Should().NotContain("Ayuda.", "los textos del manual están resueltos (sin claves de recursos)");

        // No anuncia módulos futuros como terminados: el manual aclara que el
        // registro de entrenamientos y el seguimiento de progreso aún no están
        // disponibles en la versión web.
        html.Should().Contain("todavía no están disponibles");
    }

    [Fact]
    public async Task Ayuda_ApareceEnElMenuDelAvatar_YAbreDesdeLaSesion()
    {
        var email = EmailUnico("ayuda-menu");
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, "Ana Practica", email);

        var home = await cliente.GetStringAsync("/");
        home.Should().Contain("Ayuda");
        home.Should().Contain("/Ayuda", "el menú del avatar enlaza a la ayuda real");
        home.Should().NotContain("Editar perfil", "la edición vive en «Mi perfil»");

        var respuesta = await cliente.GetAsync("/Ayuda");
        ((int)respuesta.StatusCode).Should().Be(200);
    }
}
