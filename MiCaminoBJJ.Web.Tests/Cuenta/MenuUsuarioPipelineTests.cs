using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MiCaminoBJJ.Web.Identity;
using MiCaminoBJJ.Web.Tests.Pipeline;

namespace MiCaminoBJJ.Web.Tests.Cuenta;

/// <summary>
/// Etapa 1 — encabezado y menú de perfil sobre el pipeline HTTP REAL.
///
/// Cubre: saludo + rol desde la identidad autenticada, avatar con menú
/// accesible (aria-expanded, role="menu"), ocultamiento de los accesos
/// administrativos para prácticos, visibilidad del panel para cuentas
/// Identity con rol Administrador, conservación del cierre de sesión por
/// POST con antiforgery y el estado visitante (sin bloque de usuario).
/// Base AISLADA: nunca escribe en la base principal.
/// </summary>
[Collection("SqlServerLocalDbPipeline")]
public class MenuUsuarioPipelineTests : IClassFixture<SeguridadPipelineTests.Factory>
{
    private const string Password = "Abcdef1!";

    private readonly SeguridadPipelineTests.Factory _factory;

    public MenuUsuarioPipelineTests(SeguridadPipelineTests.Factory factory) => _factory = factory;

    private HttpClient CrearCliente() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<string> TokenDe(HttpClient cliente, string url)
    {
        var html = await cliente.GetStringAsync(url);
        var match = Regex.Match(html, "__RequestVerificationToken[^>]*value=\"([^\"]+)\"");
        match.Success.Should().BeTrue($"la página {url} debe emitir un token antiforgery");
        return match.Groups[1].Value;
    }

    /// <summary>Registro + login reales (cookie MiCaminoBJJ.Cuenta) sobre la base aislada.</summary>
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

    private static async Task LoginAsync(HttpClient cliente, string email)
    {
        var token = await TokenDe(cliente, "/Cuenta/Login");
        var login = await cliente.PostAsync("/Cuenta/Login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = Password,
                ["__RequestVerificationToken"] = token
            }));
        ((int)login.StatusCode).Should().Be(302, "el login válido redirige");
    }

    /// <summary>
    /// Cuenta Identity con rol Administrador en la base AISLADA del pipeline
    /// (no toca la base principal ni usa init-admin).
    /// </summary>
    private async Task<string> CrearAdministradorIdentityAsync()
    {
        using var alcance = _factory.Services.CreateScope();
        var sp = alcance.ServiceProvider;

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("Administrador"))
            (await roleManager.CreateAsync(new IdentityRole("Administrador"))).Succeeded.Should().BeTrue();

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"menu-admin-{Guid.NewGuid():N}@example.com";
        var creacion = await userManager.CreateAsync(
            new ApplicationUser { UserName = email, Email = email }, Password);
        creacion.Succeeded.Should().BeTrue(string.Join("; ", creacion.Errors.Select(e => e.Description)));

        var usuario = await userManager.FindByEmailAsync(email);
        (await userManager.AddToRoleAsync(usuario!, "Administrador")).Succeeded.Should().BeTrue();
        return email;
    }

    private static string? FormularioDe(string html, string accion)
    {
        var match = Regex.Match(html, $"<form[^>]*{Regex.Escape(accion)}[^>]*>");
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// Texto visible de una página: elimina las etiquetas HTML, decodifica las
    /// entidades (Razor emite &#xE1; etc.) y colapsa los espacios, de modo que
    /// un saludo envuelto en spans anidados se compara como lo ve la persona.
    /// </summary>
    private static string TextoVisible(string html)
    {
        var sinEtiquetas = Regex.Replace(html, "<[^>]+>", " ");
        return Regex.Replace(WebUtility.HtmlDecode(sinEtiquetas), @"\s+", " ").Trim();
    }

    /// <summary>
    /// Fragmento del desplegable del avatar (entre el botón y el cierre del
    /// menú): permite verificar las OPCIONES del menú sin confundirlo con
    /// otros textos de la página (por ejemplo, las insignias «Próximamente»
    /// del landing, que son de las funcionalidades futuras).
    /// </summary>
    private static string BloqueDelMenu(string html)
    {
        var inicio = html.IndexOf("id=\"mccjUserMenu\"", StringComparison.Ordinal);
        if (inicio < 0)
            return string.Empty;

        var fin = html.IndexOf("</div>", inicio, StringComparison.Ordinal);
        return fin < 0 ? html[inicio..] : html[inicio..fin];
    }

    // ── 1. Visitante: sin bloque de usuario ───────────────────────────────

    [Fact]
    public async Task Visitante_Encabezado_NoMuestraElBloqueDeUsuario()
    {
        using var cliente = CrearCliente();
        var home = await cliente.GetStringAsync("/");

        // Los textos vienen del catálogo de recursos y Razor los codifica:
        // se comparan sobre el HTML decodificado.
        var pagina = WebUtility.HtmlDecode(home);

        home.Should().NotContain("mccjUserMenuBtn", "sin sesión no hay avatar ni menú de perfil");
        pagina.Should().NotContain("Bienvenido de nuevo");
        pagina.Should().Contain("Iniciar sesión", "la rama visitante conserva sus accesos");
        pagina.Should().Contain("Registrarse");
    }

    // ── 2. Práctico: saludo + menú SIN accesos administrativos ───────────

    [Fact]
    public async Task Practico_Encabezado_MuestraSaludoAvatarYMenú_SinAccesoAdmin()
    {
        var email = $"menu-practico-{Guid.NewGuid():N}@example.com";
        using var cliente = CrearCliente();
        await RegistrarYLoginAsync(cliente, "Prueba Etapa1", email);

        var home = await cliente.GetStringAsync("/");

        // Saludo y rol tomados de la identidad autenticada + su perfil.
        // El saludo envuelve el nombre en un <span> anidado (estilo del navbar) y
        // el markup se parte en varias líneas: se compara el TEXTO VISIBLE
        // (etiquetas fuera, entidades decodificadas, espacios colapsados).
        TextoVisible(home).Should().Contain("Bienvenido de nuevo, Prueba Etapa1");
        home.Should().Contain("Rol: Practicante");
        home.Should().Contain("mccj-user-greeting-mobile", "en móvil se muestra la versión abreviada");

        // Avatar + menú accesible.
        home.Should().Contain("id=\"mccjUserMenuBtn\"");
        home.Should().Contain("aria-haspopup=\"menu\"");
        home.Should().Contain("aria-expanded=\"false\"");
        home.Should().Contain("id=\"mccjUserMenu\"");
        home.Should().Contain("role=\"menu\"");
        home.Should().Contain("hidden", "el menú nace cerrado");

        // Opciones del menú: «Mi perfil» es la ÚNICA pantalla de perfil
        // (ya no existe «Editar perfil» como opción separada) y «Ayuda» ya
        // está disponible (enlace real al manual).
        // OJO: los textos del menú vienen del catálogo de recursos y Razor los
        // codifica (Configuraci&#xF3;n): se comparan sobre el HTML decodificado.
        var menu = BloqueDelMenu(WebUtility.HtmlDecode(home));
        menu.Should().Contain("Mi perfil");
        menu.Should().NotContain("Editar perfil");
        menu.Should().Contain("Configuración");
        menu.Should().Contain("Ayuda");
        menu.Should().Contain("/Ayuda");
        menu.Should().NotContain("Próximamente",
            "la ayuda ya existe: no se anuncia como módulo futuro");
        home.Should().Contain("mccj-user-menu");

        // Un práctico JAMÁS ve el acceso administrativo.
        home.Should().NotContain("Panel de administración");
        home.Should().NotContain("/AdminTecnicas");

        // Cierre de sesión: POST + antiforgery conservados dentro del menú.
        var formLogout = FormularioDe(home, "Cuenta/Logout");
        formLogout.Should().NotBeNull("el menú conserva el cierre de sesión por POST");
        formLogout!.Should().Contain("method=\"post\"");
        home.Should().Contain("__RequestVerificationToken");
        home.Should().NotContain("Cuenta/Registro", "sin rama anónima con sesión activa");
    }

    // ── 3. Administrador de Identity: acceso al panel ─────────────────────

    [Fact]
    public async Task Administrador_Encabezado_MuestraElAccesoAlPanel()
    {
        var email = await CrearAdministradorIdentityAsync();
        using var cliente = CrearCliente();
        await LoginAsync(cliente, email);

        var home = await cliente.GetStringAsync("/");

        // Los textos del menú vienen del catálogo de recursos (Razor los
        // codifica): se comparan sobre el HTML decodificado.
        var menu = WebUtility.HtmlDecode(home);
        menu.Should().Contain("Rol: Administrador",
            "el rol visible proviene de los claims de rol de la identidad, no del perfil numérico");
        menu.Should().Contain("Panel de administración");
        home.Should().Contain("/AdminTecnicas", "reutiliza la ruta existente del CRUD");
        menu.Should().Contain("Cerrar sesión", "el menú completo sigue disponible");
    }

    // ── 4. JavaScript del menú: accesible y sin almacenamiento/envíos ────

    [Fact]
    public void JavaScriptDelMenu_EsAccesible_SinAlmacenamientoNiEnvios()
    {
        var js = LeerArchivoWeb("wwwroot", "js", "site.js");

        js.Should().Contain("Escape", "cierra el menú con Escape y devuelve el foco");
        js.Should().Contain("aria-expanded", "sincroniza el estado accesible");
        js.Should().Contain("focusout", "cierra cuando el foco sale del menú");
        js.Should().NotContain("localStorage", "no se almacena nada del lado del cliente");
        js.Should().NotContain("sessionStorage");
        js.Should().NotContain("fetch(", "no se envía nada al servidor");
    }

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

        throw new FileNotFoundException("No se encontró " + string.Join("/", partesRelativas) + " desde " + AppContext.BaseDirectory);
    }
}
