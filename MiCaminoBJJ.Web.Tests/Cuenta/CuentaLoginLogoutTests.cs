using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Web.Controllers;
using MiCaminoBJJ.Web.Identity;
using MiCaminoBJJ.Web.Localization;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Services.Perfil;
using MiCaminoBJJ.Web.Tests.Infrastructure;
using MiCaminoBJJ.Web.ViewModels;

namespace MiCaminoBJJ.Web.Tests.Cuenta;

/// <summary>
/// Etapa 4C — login/logout de prácticos con DI real y emisión REAL de la
/// cookie Identity (MiCaminoBJJ.Cuenta) sobre DefaultHttpContext.
/// cubre: login correcto, mensajes genéricos (sin enumeración), bloqueo por
/// intentos, protección contra open redirect, logout que cierra SOLO la
/// sesión Identity y metadatos antiforgery de CuentaController.
///
/// Aislamiento: base ÚNICA POR EJECUCIÓN (<see cref="TestDatabaseLease"/>); la
/// limpieza es la eliminación de esa base, validada contra la conexión efectiva.
/// </summary>
[Collection("SqlServerLocalDbCuenta")]
public class CuentaLoginLogoutTests : IClassFixture<CuentaLoginLogoutTests.Fixture>
{
    public sealed class Fixture : IAsyncLifetime
    {
        private readonly List<IServiceScope> _alcances = new();

        /// <summary>Base aislada EXCLUSIVA de esta ejecución.</summary>
        public TestDatabaseLease Lease { get; } = TestDatabaseLease.Create("CuentaLogin");

        public ServiceProvider Provider { get; private set; } = null!;
        public IHttpContextAccessor Accesor { get; private set; } = null!;
        public string EmailCompartido { get; } = $"login-tfi-{Guid.NewGuid():N}@example.com";
        public string PasswordCompartido { get; } = "Abcdef1!";

        /// <summary>Alcance POR controller: evita contextos cautivos entre pruebas.</summary>
        public IServiceScope CrearAlcance()
        {
            var alcance = Provider.CreateScope();
            _alcances.Add(alcance);
            return alcance;
        }

        /// <summary>
        /// Los emails adicionales son únicos (guid) y la limpieza es la
        /// eliminación de la base aislada al cierre: no se borran filas sueltas.
        /// </summary>
        public void RegistrarEmailAdicional(string email) => _ = email;

        public async Task InitializeAsync()
        {
            // Base AISLADA verificada y ÚNICA (nunca la principal): ver TestDatabaseGuard.
            var cadena = Lease.ConnectionString;
            TestDatabaseGuard.EnsureIsolated(cadena);

            // Esquema propio: exige que la base NO existiera y la crea desde migraciones.
            await Lease.PrepareAsync();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MiCaminoBJJWeb"] = cadena
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection();
            services.AddHttpContextAccessor();
            services.AddWebTfPersistence(configuration);
            services.AddWebIdentity();
            services.AddAdminAuth();
            services.AddWebLocalization(); // localizador de textos (es/en/pt)

            // Servicio de la foto de perfil (misma implementación que la app):
            // la carpeta de almacenamiento es TEMPORAL (no escribe en el repo).
            services.AddOptions<PerfilFotoOpciones>().Configure(opciones =>
                opciones.Ruta = Path.Combine(Path.GetTempPath(), "MiCaminoBJJ.Web.Tests", Guid.NewGuid().ToString("N")));
            services.AddScoped<IPerfilFotoService, PerfilFotoService>();

            Provider = services.BuildServiceProvider();
            Accesor = Provider.GetRequiredService<IHttpContextAccessor>();

            // Usuario Identity compartido del fixture (con su perfil de dominio).
            using var alcance = Provider.CreateScope();
            var userManager = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
            TestDatabaseGuard.EnsureIsolated(db, Lease.DatabaseName);

            var identity = new ApplicationUser { UserName = EmailCompartido, Email = EmailCompartido };
            var creacion = await userManager.CreateAsync(identity, PasswordCompartido);
            if (!creacion.Succeeded)
                throw new InvalidOperationException("No se pudo crear el usuario de prueba: " + string.Join("; ", creacion.Errors.Select(e => e.Description)));

            db.Users.Add(new MiCaminoBJJ.Core.Entities.User
            {
                Name = "Login TFI",
                Email = EmailCompartido,
                PasswordHash = string.Empty,
                PasswordSalt = string.Empty,
                BeltRank = "Blanco",
                Role = MiCaminoBJJ.Core.Enums.UserRole.Practicante,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        public async Task DisposeAsync()
        {
            foreach (var alcancePrueba in _alcances)
                alcancePrueba.Dispose();

            await Provider.DisposeAsync();

            // Limpieza limitada al recurso creado por esta ejecución.
            await Lease.DropAsync();
        }
    }

    private readonly Fixture _fixture;

    public CuentaLoginLogoutTests(Fixture fixture) => _fixture = fixture;

    private CuentaController CrearController(HttpContext http)
    {
        var alcance = _fixture.CrearAlcance();
        var sp = alcance.ServiceProvider;
        _fixture.Accesor.HttpContext = http;
        http.RequestServices = sp;

        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        // ActivatorUtilities: resuelve TODAS las dependencias del constructor
        // desde el contenedor (evita acoplar la prueba a la firma exacta).
        var controller = ActivatorUtilities.CreateInstance<CuentaController>(sp);
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new VacioTempDataProvider());
        controller.Url = new UrlHelper(actionContext);
        return controller;
    }

    private static HttpContext HttpContextFresco() =>
        new DefaultHttpContext { Response = { Body = new MemoryStream() } };

    private static List<string> CookiesDe(HttpContext http)
    {
        if (!http.Response.Headers.TryGetValue("Set-Cookie", out var valores) || valores.Count == 0)
            return new List<string>();

        return valores.Select(v => v ?? string.Empty).ToList();
    }

    // ── Login correcto ────────────────────────────────────────────────────

    [Fact]
    public async Task Login_Correcto_EmiteCookieIdentity_YRedirigeALocal()
    {
        var http = HttpContextFresco();
        var controller = CrearController(http);

        var resultado = await controller.Login(new LoginViewModel
        {
            Email = _fixture.EmailCompartido,
            Password = _fixture.PasswordCompartido,
            ReturnUrl = "/Tecnicas"
        });

        var redirect = resultado.Should().BeOfType<LocalRedirectResult>().Subject;
        redirect.Url.Should().Be("/Tecnicas", "las rutas locales del returnUrl se conservan");

        var cookies = CookiesDe(http);
        cookies.Should().Contain(c => c.Contains(IdentitySetup.AppCookieName),
            "debe emitirse la cookie de Identity (MiCaminoBJJ.Cuenta)");
        cookies.Should().NotContain(c => c.Contains("MiCaminoBJJ.Admin"),
            "el login de práctico NO emite ni usa la cookie administrativa");
    }

    // ── Mensajes genéricos (sin enumeración) ──────────────────────────────

    [Fact]
    public async Task Login_Incorrecto_YEmailDesconocido_MuestranElMismoMensajeGenerico()
    {
        // Contraseña incorrecta (email existente).
        var http1 = HttpContextFresco();
        var c1 = CrearController(http1);
        var r1 = await c1.Login(new LoginViewModel { Email = _fixture.EmailCompartido, Password = "Otra-123!" });
        r1.Should().BeOfType<ViewResult>();
        var mensaje1 = c1.ModelState[string.Empty]!.Errors.Single().ErrorMessage;
        var cookies1 = CookiesDe(http1);

        // Email inexistente.
        var http2 = HttpContextFresco();
        var c2 = CrearController(http2);
        var r2 = await c2.Login(new LoginViewModel { Email = "nadie-existe-tfi@example.com", Password = "Otra-123!" });
        r2.Should().BeOfType<ViewResult>();
        var mensaje2 = c2.ModelState[string.Empty]!.Errors.Single().ErrorMessage;
        var cookies2 = CookiesDe(http2);

        mensaje1.Should().Be(mensaje2,
            "un atacante no puede distinguir una cuenta inexistente de una contraseña incorrecta");
        cookies1.Should().BeEmpty("sin cookie con credenciales inválidas");
        cookies2.Should().BeEmpty();
    }

    // ── Bloqueo por intentos ──────────────────────────────────────────────

    [Fact]
    public async Task Login_BloqueaLaCuenta_TrasIntentosFallidos()
    {
        // Usuario PROPIO del test: el bloqueo no debe afectar al usuario
        // compartido del fixture ni a otras pruebas (el orden no está garantizado).
        var email = $"bloqueo-tfi-{Guid.NewGuid():N}@example.com";
        var password = "Abcdef1!";
        _fixture.RegistrarEmailAdicional(email);

        using (var alcance = _fixture.CrearAlcance())
        {
            var um = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var creacion = await um.CreateAsync(new ApplicationUser { UserName = email, Email = email }, password);
            creacion.Succeeded.Should().BeTrue();
        }

        var sp = _fixture.Provider;
        using (var alcance = sp.CreateScope())
        {
            var um = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var maximos = um.Options.Lockout.MaxFailedAccessAttempts;

            // Un controller FRESCO por intento (cada petición HTTP es un request nuevo;
            // reutilizarlo arrastraba un ModelState inválido que saltaba el login).
            for (var i = 0; i < maximos; i++)
            {
                var intento = CrearController(HttpContextFresco());
                var r = await intento.Login(new LoginViewModel { Email = email, Password = "Incorrecta-" + i + "!" });
                r.Should().BeOfType<ViewResult>();
            }

            // Incluso con la contraseña CORRECTA, la cuenta queda bloqueada.
            var controller = CrearController(HttpContextFresco());
            var resultado = await controller.Login(new LoginViewModel { Email = email, Password = password });

            resultado.Should().BeOfType<ViewResult>();
            controller.ModelState[string.Empty]!.Errors.Single().ErrorMessage
                .Should().Contain("bloqueada", "el bloqueo temporal debe respetarse y comunicarse");

            var identity = await um.FindByEmailAsync(email);
            (await um.IsLockedOutAsync(identity!)).Should().BeTrue();

            // Identity RESETEA AccessFailedCount cuando el bloqueo se activa
            // (ventana limpia para el próximo ciclo): la prueba valida el
            // LockoutEnd futuro, que es la evidencia real del bloqueo.
            var lockoutEnd = await um.GetLockoutEndDateAsync(identity!);
            lockoutEnd.Should().NotBeNull("el bloqueo debe dejar una fecha de fin");
            lockoutEnd!.Value.UtcDateTime.Should().BeAfter(DateTime.UtcNow,
                "la cuenta debe quedar bloqueada temporalmente (ventana de 5 minutos)");
        }
    }

    // ── Open redirect ─────────────────────────────────────────────────────

    [Fact]
    public async Task Login_RedirigeSoloALocales()
    {
        // Externa → cae en "/".
        var c1 = CrearController(HttpContextFresco());
        var r1 = await c1.Login(new LoginViewModel
        {
            Email = _fixture.EmailCompartido,
            Password = _fixture.PasswordCompartido,
            ReturnUrl = "https://ejemplo-malo.com/robar"
        });
        r1.Should().BeOfType<LocalRedirectResult>().Which.Url.Should().Be("/");

        // Local → se conserva.
        var c2 = CrearController(HttpContextFresco());
        var r2 = await c2.Login(new LoginViewModel
        {
            Email = _fixture.EmailCompartido,
            Password = _fixture.PasswordCompartido,
            ReturnUrl = "/Tecnicas?buscar=kimura"
        });
        r2.Should().BeOfType<LocalRedirectResult>().Which.Url.Should().Be("/Tecnicas?buscar=kimura");
    }

    // ── Logout ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_CierraSoloLaSesiónIdentity()
    {
        // Login previo para tener cookie.
        var http = HttpContextFresco();
        var login = CrearController(http);
        await login.Login(new LoginViewModel { Email = _fixture.EmailCompartido, Password = _fixture.PasswordCompartido });

        // Logout con el mismo contexto (misma cookie).
        var logout = CrearController(http);
        var resultado = await logout.Logout();

        var redirect = resultado.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        redirect.ControllerName.Should().Be("Home", "el cierre vuelve a la Home pública");

        var cookies = CookiesDe(http);
        cookies.Should().Contain(c => c.Contains(IdentitySetup.AppCookieName),
            "la cookie Identity se invalida (expiración pasada)");
        cookies.Should().NotContain(c => c.Contains("MiCaminoBJJ.Admin"),
            "la sesión administrativa NO se toca desde el logout de prácticos");
    }

    // ── Metadatos antiforgery ─────────────────────────────────────────────

    [Fact]
    public void Cuenta_TodosLosPost_TienenProteccionAntiforgery()
    {
        var post = typeof(CuentaController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() is not null)
            .ToList();

        // El conteo exacto puede crecer con nuevas pantallas: lo que NO puede
        // faltar es la protección antiforgery de TODOS los POST.
        post.Count.Should().BeGreaterThanOrEqualTo(4, "Registro, Login, Logout y Perfil (guardar cambios) son POST");
        post.Select(m => m.Name).Should().Contain(new[] { "Registro", "Login", "Logout", "Perfil" });

        foreach (var accion in post)
        {
            Assert.True(
                accion.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null,
                $"{accion.Name} debe declarar [ValidateAntiForgeryToken]");
        }
    }

    private sealed class VacioTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }
}
