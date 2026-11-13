using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Web.Identity;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Tests.Infrastructure;

namespace MiCaminoBJJ.Web.Tests.Pipeline;

/// <summary>
/// Etapa 4C.1 — pruebas de SEGURIDAD sobre el pipeline HTTP REAL
/// (WebApplicationFactory: cookies, antiforgery, políticas y gate reales).
///
/// Verifican el aislamiento entre la autenticación de prácticos (Identity,
/// cookie MiCaminoBJJ.Cuenta) y el administrativo temporal de Development
/// (esquema "Cookies", cookie MiCaminoBJJ.Admin) SIN depender de la
/// visibilidad de botones ni de middleware manual de User.
///
/// Aislamiento (Fase 1-3): cada fábrica arrienda una base ÚNICA POR EJECUCIÓN
/// (<see cref="TestDatabaseLease"/>). El nombre nunca se reutiliza, no se
/// elimina ninguna base preexistente y la limpieza valida la conexión EFECTIVA
/// contra el nombre exacto del arrendamiento.
/// </summary>
[Collection("SqlServerLocalDbPipeline")]
public class SeguridadPipelineTests : IClassFixture<SeguridadPipelineTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        public List<string> EmailsCreados { get; } = new();

        /// <summary>
        /// Variable de entorno que SOBRESCRIBE la cadena de appsettings.json.
        /// Es la ÚNICA vía fiable con hosting mínimo: `ConfigureAppConfiguration`
        /// de WebApplicationFactory se aplica en `builder.Build()`, es decir
        /// DESPUÉS de que Program.cs ya registró AddWebTfPersistence con la
        /// configuración original (por eso las pruebas antiguas escribían en la
        /// base principal).
        /// </summary>
        public const string VariableConexion = "ConnectionStrings__MiCaminoBJJWeb";

        /// <summary>
        /// Clave del proveedor de IA forzada a vacío en pruebas: ningún test
        /// puede disparar una llamada real pagada (por encima de User Secrets).
        /// </summary>
        public const string VariableGuiaApiKey = "GuiaPracticante__ApiKey";

        /// <summary>Base AISLADA y ÚNICA del pipeline (nunca la principal).</summary>
        public TestDatabaseLease Lease { get; } = TestDatabaseLease.Create("Pipeline");

        public Factory()
        {
            // Esquema propio ANTES de que la app arranque (Program.cs siembra
            // la biblioteca en Development).
            Lease.Prepare();

            // Fijada ANTES de construir el host: Program.cs leerá ESTA cadena.
            Environment.SetEnvironmentVariable(VariableConexion, Lease.ConnectionString);

            // Etapa 5: modo catálogo obligatorio en pruebas (sin IA real).
            Environment.SetEnvironmentVariable(VariableGuiaApiKey, string.Empty);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseEnvironment("Development");

        /// <summary>
        /// Limpieza controlada en el cierre: elimina la base aislada PROPIA de
        /// este arrendamiento (valida conexión efectiva + nombre exacto). No usa
        /// el ServiceProvider del host, que ya está en liberación.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                    Lease.Drop(); // una limpieza fallida se propaga (nunca se silencia)
            }
            finally
            {
                Environment.SetEnvironmentVariable(VariableConexion, null);
                Environment.SetEnvironmentVariable(VariableGuiaApiKey, null);
                base.Dispose(disposing);
            }
        }
    }

    /// <summary>
    /// Fábrica INDEPENDIENTE en Production: con WithWebHostBuilder conviviría
    /// con el UseEnvironment("Development") de la fábrica base y el gate no
    /// se activaría. Aquí el entorno es inequívoco.
    /// </summary>
    public sealed class FactoryProduccion : WebApplicationFactory<Program>
    {
        /// <summary>Base aislada PROPIA (no comparte con la fábrica de Development).</summary>
        public TestDatabaseLease Lease { get; } = TestDatabaseLease.Create("PipelineProduccion");

        public FactoryProduccion()
        {
            Lease.Prepare();
            Environment.SetEnvironmentVariable(Factory.VariableConexion, Lease.ConnectionString);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseEnvironment("Production");

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                    Lease.Drop(); // una limpieza fallida se propaga (nunca se silencia)
            }
            finally
            {
                Environment.SetEnvironmentVariable(Factory.VariableConexion, null);
                base.Dispose(disposing);
            }
        }
    }

    private readonly Factory _factory;

    public SeguridadPipelineTests(Factory factory) => _factory = factory;

    // ── Helpers del pipeline ──────────────────────────────────────────────

    private HttpClient CrearCliente(bool seguirRedirecciones = false) =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = seguirRedirecciones
        });

    private async Task<string> TokenDe(HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"");
        match.Success.Should().BeTrue($"la página {url} debe emitir un token antiforgery");
        return match.Groups[1].Value;
    }

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url, Dictionary<string, string> datos) =>
        await client.PostAsync(url, new FormUrlEncodedContent(datos));

    private string EmailUnico(string prefijo) => $"pipeline-{prefijo}-{Guid.NewGuid():N}@example.com";

    private async Task RegistrarPracticoAsync(HttpClient client, string email, string password)
    {
        var token = await TokenDe(client, "/Cuenta/Registro");
        var respuesta = await PostFormAsync(client, "/Cuenta/Registro", new Dictionary<string, string>
        {
            ["Nombre"] = "Prueba Pipeline",
            ["Email"] = email,
            ["Cinturon"] = "Blanco",
            ["Password"] = password,
            ["ConfirmarPassword"] = password,
            ["__RequestVerificationToken"] = token
        });
        ((int)respuesta.StatusCode).Should().Be(302, "el registro válido redirige al login");
        _factory.EmailsCreados.Add(email);
    }

    private async Task LoginPracticoAsync(HttpClient client, string email, string password)
    {
        var token = await TokenDe(client, "/Cuenta/Login");
        var respuesta = await PostFormAsync(client, "/Cuenta/Login", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token
        });
        ((int)respuesta.StatusCode).Should().Be(302, "el login válido redirige");
    }

    private async Task LoginAdminAsync(HttpClient client)
    {
        var password = _factory.Services.GetRequiredService<IConfiguration>()["AdminDev:Password"];
        password.Should().NotBeNullOrEmpty("la credencial de Development debe existir en User Secrets");

        var token = await TokenDe(client, "/AdminAcceso/Login");
        var respuesta = await PostFormAsync(client, "/AdminAcceso/Login", new Dictionary<string, string>
        {
            ["Password"] = password!,
            ["ReturnUrl"] = "/AdminTecnicas",
            ["__RequestVerificationToken"] = token
        });
        ((int)respuesta.StatusCode).Should().Be(302, "el login admin válido redirige");
    }

    private (string Email, string Password) NuevoPractico()
    {
        var email = EmailUnico("flujo");
        return (email, "Abcdef1!");
    }

    // ── 1/12. Anónimo: challenge coherente y rutas públicas ───────────────

    [Fact]
    public async Task Anonimo_GetAdmin_RedirigeAlLoginAdmin()
    {
        using var cliente = CrearCliente();
        var respuesta = await cliente.GetAsync("/AdminTecnicas");

        ((int)respuesta.StatusCode).Should().Be((int)HttpStatusCode.Redirect,
            "el challenge recorre los dos esquemas de la política (Identity y Cookies) y el Location final es el del login admin");
        respuesta.Headers.Location!.ToString().Should().Contain("/AdminAcceso/Login");
    }

    [Fact]
    public async Task Anonimo_LasRutasPublicas_SeConservanDisponibles()
    {
        using var cliente = CrearCliente();
        foreach (var ruta in new[] { "/", "/Tecnicas", "/Cuenta/Registro", "/Cuenta/Login" })
        {
            var respuesta = await cliente.GetAsync(ruta);
            ((int)respuesta.StatusCode).Should().Be(200, $"{ruta} es pública");
        }
    }

    // ── Registro con cinturón obligatorio + perfil del práctico ────────────

    [Fact]
    public async Task Registro_SinCinturon_NoRedirige_NoCreaCuenta()
    {
        using var cliente = CrearCliente();
        var token = await TokenDe(cliente, "/Cuenta/Registro");
        var email = EmailUnico("sincinturon");

        var respuesta = await PostFormAsync(cliente, "/Cuenta/Registro", new Dictionary<string, string>
        {
            ["Nombre"] = "Prueba Sin Cinturon",
            ["Email"] = email,
            ["Password"] = "Abcdef1!",
            ["ConfirmarPassword"] = "Abcdef1!",
            ["__RequestVerificationToken"] = token
        });

        ((int)respuesta.StatusCode).Should().Be(200, "sin cinturón el servidor devuelve el formulario con error");
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        html.Should().Contain("Seleccioná tu cinturón");

        using var alcance = _factory.Services.CreateScope();
        var userManager = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.FindByEmailAsync(email)).Should().BeNull("no se crea ninguna cuenta sin cinturón");
    }

    [Fact]
    public async Task Anonimo_GetPerfil_RedirigeAlLoginDePractico()
    {
        using var cliente = CrearCliente();
        var respuesta = await cliente.GetAsync("/Cuenta/Perfil");

        ((int)respuesta.StatusCode).Should().Be((int)HttpStatusCode.Redirect,
            "la política UsuarioAutenticado desafía con la cookie Identity");
        respuesta.Headers.Location!.ToString().Should().Contain("/Cuenta/Login");
    }

    [Fact]
    public async Task Practicante_GetPerfil_MuestraSuCinturonYSuRol()
    {
        var (email, password) = NuevoPractico();
        using var cliente = CrearCliente();
        await RegistrarPracticoAsync(cliente, email, password);
        await LoginPracticoAsync(cliente, email, password);

        var respuesta = await cliente.GetAsync("/Cuenta/Perfil");
        ((int)respuesta.StatusCode).Should().Be(200);

        var html = await respuesta.Content.ReadAsStringAsync();
        html.Should().Contain("Mi perfil");
        html.Should().Contain("Blanco", "muestra el cinturón guardado en el perfil");
        html.Should().Contain("Practicante", "el rol visible sigue siendo Practicante");
        html.Should().NotContain("Administrador", "el perfil no muestra ni otorga roles administrativos");
    }

    // ── 2/3/4. Práctico: funciones permitidas y rechazo del CRUD ──────────

    [Fact]
    public async Task Practicante_AccedeAHomeYBiblioteca_YNavbarMuestraSuEstado()
    {
        var (email, password) = NuevoPractico();
        using var cliente = CrearCliente();
        await RegistrarPracticoAsync(cliente, email, password);
        await LoginPracticoAsync(cliente, email, password);

        foreach (var ruta in new[] { "/", "/Tecnicas" })
        {
            var respuesta = await cliente.GetAsync(ruta);
            ((int)respuesta.StatusCode).Should().Be(200);
        }

        var home = await cliente.GetStringAsync("/");
        // Los textos de la navbar vienen del catálogo de recursos y Razor los
        // codifica (Cerrar sesi&#xF3;n): se comparan sobre el HTML decodificado.
        WebUtility.HtmlDecode(home).Should().Contain("Cerrar sesión", "la navbar debe mostrar el estado del práctico");
        home.Should().NotContain("Cuenta/Registro", "sin rama anónima con sesión activa");
    }

    [Fact]
    public async Task Practicante_NoPuedeAcceder_NiEscribirEnElCRUD()
    {
        var (email, password) = NuevoPractico();
        using var cliente = CrearCliente();
        await RegistrarPracticoAsync(cliente, email, password);
        await LoginPracticoAsync(cliente, email, password);

        var get = await cliente.GetAsync("/AdminTecnicas");
        ((int)get.StatusCode).Should().Be((int)HttpStatusCode.Redirect,
            "la política autentica los DOS esquemas: el práctico se autentica por Identity pero no tiene el rol Administrador → AccessDenied");
        get.Headers.Location!.ToString().Should().Contain("/Cuenta/AccesoDenegado");

        // La página de acceso denegado existe y es navegable (sin bucle).
        var denegado = await cliente.GetAsync(get.Headers.Location!.ToString());
        ((int)denegado.StatusCode).Should().Be(200);
        (await denegado.Content.ReadAsStringAsync()).Should().Contain("Acceso denegado");

        var getCreate = await cliente.GetAsync("/AdminTecnicas/Create");
        ((int)getCreate.StatusCode).Should().Be((int)HttpStatusCode.Redirect);

        // POST administrativo con sesión de práctico: denegado y sin escritura.
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        var tecnica = await db.Techniques.FirstAsync();
        var estadoPrevia = tecnica.IsActive;

        var post = await cliente.PostAsync($"/AdminTecnicas/Deactivate/{tecnica.Id}", new FormUrlEncodedContent(new Dictionary<string, string>()));
        ((int)post.StatusCode).Should().NotBe(200, "un práctico jamás ejecuta POST administrativos");

        (await db.Techniques.AsNoTracking().CountAsync(t => t.Id == tecnica.Id && t.IsActive == estadoPrevia)).Should().Be(1,
            "la operación denegada no debe modificar datos");
    }

    [Fact]
    public async Task Practico_NoPuedeEjecutarAccionesMultimedia_DeTecnicas()
    {
        var (email, password) = NuevoPractico();
        using var cliente = CrearCliente();
        await RegistrarPracticoAsync(cliente, email, password);
        await LoginPracticoAsync(cliente, email, password);

        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        var tecnica = await db.Techniques.FirstAsync();
        var mediaPrevia = await db.TechniqueMedia.CountAsync(tm => tm.TechniqueId == tecnica.Id);

        // Las 4 acciones multimedia comparten la política AdministradorContenido.
        foreach (var accion in new[] { "Agregar", "Editar", "MarcarPortada", "Quitar" })
        {
            var post = await cliente.PostAsync(
                $"/AdminTecnicasMedia/{accion}/{tecnica.Id}",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Url"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                    ["mediaId"] = "1"
                }));

            ((int)post.StatusCode).Should().NotBe(200,
                $"{accion} con sesión de práctico debe quedar denegado");
        }

        (await db.TechniqueMedia.CountAsync(tm => tm.TechniqueId == tecnica.Id)).Should().Be(mediaPrevia,
            "ninguna acción multimedia denegada debe escribir en la base");
    }

    // ── 5/6. Separación de cookies y sin escalada ─────────────────────────

    /// <summary>
    /// Crea (en la base AISLADA del pipeline) un usuario Identity con el rol
    /// Administrador. NO toca la base principal ni usa init-admin.
    /// </summary>
    private async Task<string> CrearAdministradorIdentityAsync()
    {
        using var alcance = _factory.Services.CreateScope();
        var sp = alcance.ServiceProvider;
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("Administrador"))
            (await roleManager.CreateAsync(new IdentityRole("Administrador"))).Succeeded.Should().BeTrue();

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var email = EmailUnico("admin-id");
        var creacion = await userManager.CreateAsync(
            new ApplicationUser { UserName = email, Email = email }, "Abcdef1!");
        creacion.Succeeded.Should().BeTrue(string.Join("; ", creacion.Errors.Select(e => e.Description)));

        var usuario = await userManager.FindByEmailAsync(email);
        (await userManager.AddToRoleAsync(usuario!, "Administrador")).Succeeded.Should().BeTrue();
        return email;
    }

    [Fact]
    public async Task AdministradorDeIdentity_AccedeAlPanelAdministrativo()
    {
        // Etapa 5: un usuario Identity con rol Administrador (sin cookie
        // temporal de Development) debe poder usar el CRUD en Development.
        var email = await CrearAdministradorIdentityAsync();

        using var cliente = CrearCliente();
        await LoginPracticoAsync(cliente, email, "Abcdef1!"); // login por Identity

        var get = await cliente.GetAsync("/AdminTecnicas");
        ((int)get.StatusCode).Should().Be(200,
            "la política AdministradorContenido acepta la sesión de Identity con rol Administrador");

        var getCreate = await cliente.GetAsync("/AdminTecnicas/Create");
        ((int)getCreate.StatusCode).Should().Be(200);

        // La navbar pública sigue distinguiendo Identity de la cookie admin.
        var home = await cliente.GetStringAsync("/");
        WebUtility.HtmlDecode(home).Should().Contain("Cerrar sesión");
    }

    [Fact]
    public async Task CookieAdmin_NoSeInterpretaComoSesionDePractico()
    {
        using var cliente = CrearCliente();
        await LoginAdminAsync(cliente);

        var home = await cliente.GetStringAsync("/");
        WebUtility.HtmlDecode(home).Should().NotContain("Cerrar sesión",
            "la cookie admin NO es una sesión de práctico (navbar en rama pública)");
        home.Should().Contain("Cuenta/Registro");

        var biblioteca = await cliente.GetAsync("/Tecnicas");
        ((int)biblioteca.StatusCode).Should().Be(200);
    }

    [Fact]
    public async Task AmbasCookies_NoProducenEscaladaDePrivilegios()
    {
        var (email, password) = NuevoPractico();
        using var cliente = CrearCliente();
        await LoginAdminAsync(cliente);
        await RegistrarPracticoAsync(cliente, email, password);
        await LoginPracticoAsync(cliente, email, password);

        // El CRUD funciona SOLO por la cookie admin (legítimo), y la navbar
        // refleja la sesión de Identity (default estándar).
        var admin = await cliente.GetAsync("/AdminTecnicas");
        ((int)admin.StatusCode).Should().Be(200, "el acceso admin sigue siendo la cookie de su esquema");

        var home = await cliente.GetStringAsync("/");
        WebUtility.HtmlDecode(home).Should().Contain("Cerrar sesión", "la sesión de Identity es visible en la UI");
    }

    // ── 6b. Listado administrativo: filtro por cinturón ───────────────────

    [Fact]
    public async Task ListadoAdmin_ExponeElFiltroDeCinturon_YElBotonDeLimpiar()
    {
        using var cliente = CrearCliente();
        await LoginAdminAsync(cliente);

        var html = WebUtility.HtmlDecode(await cliente.GetStringAsync("/AdminTecnicas"));

        html.Should().Contain("id=\"cinturon\"", "el listado tiene un tercer filtro: cinturón");
        html.Should().Contain("name=\"cinturon\"");
        html.Should().Contain(">Todos<", "la primera opción del filtro es «Todos»");
        foreach (var cinturon in new[] { "Blanco", "Azul", "Violeta", "Marrón", "Negro" })
            html.Should().Contain(cinturon, "las opciones salen del catálogo existente");

        html.Should().Contain("Limpiar filtros", "el botón de limpieza está siempre disponible");
        html.Should().Contain("name=\"buscar\"", "conserva el buscador por nombre/sinónimo");
        html.Should().Contain("name=\"estado\"", "conserva el filtro de estado");
        html.Should().Contain("en total (incluye desactivadas)", "muestra la cantidad de resultados");
    }

    [Fact]
    public async Task ListadoAdmin_SoloPorCinturon_FiltraAunqueElBuscadorEsteVacio()
    {
        using var cliente = CrearCliente();
        await LoginAdminAsync(cliente);

        var html = WebUtility.HtmlDecode(await cliente.GetStringAsync("/AdminTecnicas?cinturon=3"));

        html.Should().Contain("· cinturón Violeta", "la barra de resultados informa el filtro aplicado");
        html.Should().Contain("<td>Violeta</td>", "solo aparecen técnicas de ese cinturón");
        html.Should().NotContain("<td>Blanco</td>", "las de otros cinturones quedan fuera");
        html.Should().NotContain("<td>Negro</td>", "las de otros cinturones quedan fuera");
        html.Should().Contain("id=\"buscar\"", "el buscador sigue presente");
        html.Should().NotContain("name=\"buscar\" value=\"", "…y sin texto: el filtro es solo el cinturón");
    }

    [Fact]
    public async Task ListadoAdmin_Editar_YVolver_ConservanLosTresFiltros()
    {
        using var cliente = CrearCliente();
        await LoginAdminAsync(cliente);

        var listado = WebUtility.HtmlDecode(
            await cliente.GetStringAsync("/AdminTecnicas?buscar=a&estado=true&cinturon=3"));

        var match = Regex.Match(listado, "href=\"(/AdminTecnicas/Edit/\\d+[^\"]*)\"");
        match.Success.Should().BeTrue("cada fila ofrece «Editar» conservando los filtros");

        var urlEdicion = match.Groups[1].Value;
        urlEdicion.Should().Contain("buscar=a");
        urlEdicion.Should().Contain("estado=true");
        urlEdicion.Should().Contain("cinturon=3");

        var formulario = WebUtility.HtmlDecode(await cliente.GetStringAsync(urlEdicion));

        formulario.Should().Contain("name=\"buscar\" value=\"a\"", "los filtros viajan como ocultos");
        formulario.Should().Contain("name=\"estado\" value=\"true\"");
        formulario.Should().Contain("name=\"cinturon\" value=\"3\"");
        formulario.Should().Contain("href=\"/AdminTecnicas?buscar=a&estado=true&cinturon=3\"",
            "«Cancelar» vuelve al listado con los mismos criterios");
    }

    [Fact]
    public async Task ListadoAdmin_SinCoincidencias_MuestraElMensajeDeVacio()
    {
        using var cliente = CrearCliente();
        await LoginAdminAsync(cliente);

        var html = WebUtility.HtmlDecode(await cliente.GetStringAsync(
            $"/AdminTecnicas?buscar={Guid.NewGuid():N}&cinturon=5"));

        html.Should().Contain("Sin resultados");
        html.Should().Contain("No hay técnicas que coincidan con esos criterios");
        html.Should().Contain("elegí otro cinturón o limpiá los filtros");
        html.Should().Contain("href=\"/AdminTecnicas\"", "ofrece volver a todas");
    }

    // ── 7/8. Logout cruzado ──────────────────────────────────────────────

    [Fact]
    public async Task LogoutDePractico_NoCierraLaSesionAdministrativa()
    {
        var (email, password) = NuevoPractico();
        using var cliente = CrearCliente();
        await LoginAdminAsync(cliente);
        await RegistrarPracticoAsync(cliente, email, password);
        await LoginPracticoAsync(cliente, email, password);

        var token = await TokenDe(cliente, "/");
        var logout = await PostFormAsync(cliente, "/Cuenta/Logout", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
        ((int)logout.StatusCode).Should().Be(302);

        var admin = await cliente.GetAsync("/AdminTecnicas");
        ((int)admin.StatusCode).Should().Be(200, "la cookie admin permanece intacta");
    }

    [Fact]
    public async Task LogoutAdministrativo_NoCierraLaSesionDePractico()
    {
        var (email, password) = NuevoPractico();
        using var cliente = CrearCliente();
        await LoginAdminAsync(cliente);
        await RegistrarPracticoAsync(cliente, email, password);
        await LoginPracticoAsync(cliente, email, password);

        // Token del panel admin (sesión admin activa).
        var tokenAdmin = await TokenDe(cliente, "/AdminTecnicas");
        var logoutAdmin = await PostFormAsync(cliente, "/AdminAcceso/Logout", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tokenAdmin
        });
        ((int)logoutAdmin.StatusCode).Should().Be(302);

        // El CRUD queda cerrado (la sesión admin de Cookies ya no existe; la
        // sesión de Identity del práctico no tiene el rol Administrador →
        // AccessDenied local).
        var admin = await cliente.GetAsync("/AdminTecnicas");
        ((int)admin.StatusCode).Should().Be((int)HttpStatusCode.Redirect);
        admin.Headers.Location!.ToString().Should().Contain("/Cuenta/AccesoDenegado");

        // …y la sesión de práctico sigue viva.
        WebUtility.HtmlDecode(await cliente.GetStringAsync("/")).Should().Contain("Cerrar sesión");
    }

    // ── 9. Gate de Development ────────────────────────────────────────────

    [Fact]
    public async Task FueraDeDevelopment_LasRutasAdministrativas_Devuelven404()
    {
        using var produccion = new FactoryProduccion();
        using var cliente = produccion.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // Cadena completa de deshabilitación: el challenge (302) lleva al login
        // administrativo y ESE responde 404 fuera de Development → nada es
        // alcanzable (equivalente al 404 observado siguiendo la redirección).
        foreach (var ruta in new[] { "/AdminTecnicas", "/AdminTecnicas/Create", "/AdminAcceso/Login" })
        {
            var respuesta = await cliente.GetAsync(ruta);
            var codigo = (int)respuesta.StatusCode;

            if (codigo == (int)HttpStatusCode.Redirect)
            {
                respuesta.Headers.Location!.ToString().Should().Contain("/AdminAcceso/Login",
                    $"{ruta} solo redirige hacia el login administrativo");
                var destino = await cliente.GetAsync(respuesta.Headers.Location!.ToString());
                ((int)destino.StatusCode).Should().Be(404, "el destino existe solo en Development");
            }
            else
            {
                codigo.Should().Be(404, $"{ruta} responde 404 directamente en Production");
            }
        }

        var post = await cliente.PostAsync("/AdminTecnicas/Deactivate/1", new FormUrlEncodedContent(new Dictionary<string, string>()));
        new[] { 200, 201, 204 }.Should().NotContain((int)post.StatusCode,
            "un POST administrativo jamás se ejecuta fuera de Development");

        var home = await cliente.GetAsync("/");
        ((int)home.StatusCode).Should().Be(200, "la web pública sigue funcionando en Production");
    }

    // ── 10/11. CSRF y POST anónimo ────────────────────────────────────────

    [Fact]
    public async Task CSRF_PostAdministrativo_SinToken_EsRechazado()
    {
        using var cliente = CrearCliente();
        await LoginAdminAsync(cliente);

        var post = await cliente.PostAsync("/AdminTecnicas/Deactivate/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["otra"] = "clave"
        }));
        ((int)post.StatusCode).Should().Be(400, "el antiforgery global rechaza el POST sin token");
    }

    [Fact]
    public async Task Anonimo_POSTAdministrativo_SinCookie_EsRechazado_SinEscritura()
    {
        using var alcance = _factory.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        var tecnica = await db.Techniques.FirstAsync();
        var estadoPrevia = tecnica.IsActive;

        using var cliente = CrearCliente();
        var post = await cliente.PostAsync($"/AdminTecnicas/Deactivate/{tecnica.Id}", new FormUrlEncodedContent(new Dictionary<string, string>()));
        ((int)post.StatusCode).Should().NotBe(200);

        (await db.Techniques.AsNoTracking().CountAsync(t => t.Id == tecnica.Id && t.IsActive == estadoPrevia)).Should().Be(1);
    }

    // ── 13. Login con contraseña incorrecta ───────────────────────────────

    [Fact]
    public async Task LoginConPasswordIncorrecta_EsRechazado_SinCookie()
    {
        var (email, _) = NuevoPractico();
        using var cliente = CrearCliente();

        // Usuario inexistente: mensaje genérico sin enumeración y sin cookie.
        var token = await TokenDe(cliente, "/Cuenta/Login");
        var desconocido = await PostFormAsync(cliente, "/Cuenta/Login", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = "Incorrecta-xyz!1",
            ["__RequestVerificationToken"] = token
        });
        var htmlDesconocido = await desconocido.Content.ReadAsStringAsync();
        ((int)desconocido.StatusCode).Should().Be(200);
        htmlDesconocido.Should().Contain("incorrectos");
        desconocido.Headers.Should().NotContain(h => h.Key == "Set-Cookie" && h.Value.Any(v => v!.Contains("MiCaminoBJJ.Cuenta")));
    }

    // ── 14/15. Navbar y regresiones (registro/login/logout) ───────────────

    [Fact]
    public async Task Navbar_VuelveAlEstadoAnonimo_DespuesDeLogout()
    {
        var (email, password) = NuevoPractico();
        using var cliente = CrearCliente();
        await RegistrarPracticoAsync(cliente, email, password);
        await LoginPracticoAsync(cliente, email, password);

        var token = await TokenDe(cliente, "/");
        var logout = await PostFormAsync(cliente, "/Cuenta/Logout", new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });
        ((int)logout.StatusCode).Should().Be(302);

        var home = await cliente.GetStringAsync("/");
        home.Should().Contain("Cuenta/Login");
        WebUtility.HtmlDecode(home).Should().NotContain("Cerrar sesión");
    }
}
