using System.ComponentModel.DataAnnotations;
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
using MiCaminoBJJ.Core.Enums;
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
/// Etapa 4C — registro de prácticos contra la base exclusiva (DI real con
/// UserManager/SignInManager/RoleManager de Identity).
///
/// Aislamiento (Fase 1-3 de la corrección): el fixture usa una base
/// ÚNICA POR EJECUCIÓN (<see cref="TestDatabaseLease"/>) con prefijo
/// obligatorio; la limpieza es la eliminación de ESA base, validada contra la
/// conexión efectiva. Nunca se borran filas de una base compartida ni, por
/// supuesto, de la principal.
/// </summary>
[Collection("SqlServerLocalDbCuenta")]
public class CuentaRegistroTests : IClassFixture<CuentaRegistroTests.Fixture>
{
    public sealed class Fixture : IAsyncLifetime
    {
        private readonly List<IServiceScope> _alcances = new();

        /// <summary>Base aislada EXCLUSIVA de esta ejecución.</summary>
        public TestDatabaseLease Lease { get; } = TestDatabaseLease.Create("CuentaRegistro");

        public ServiceProvider Provider { get; private set; } = null!;
        public IHttpContextAccessor Accesor { get; private set; } = null!;

        /// <summary>
        /// Alcance DI POR CONTROLLER/PRUEBA: los servicios scoped (UserManager,
        /// SignInManager, WebTfDbContext) jamás se resuelven desde el root
        /// provider (un contexto cautivo envenenaría las entidades entre pruebas).
        /// </summary>
        public IServiceScope CrearAlcance()
        {
            var alcance = Provider.CreateScope();
            _alcances.Add(alcance);
            return alcance;
        }

        public async Task InitializeAsync()
        {
            // Base AISLADA verificada y ÚNICA (nunca la principal): ver TestDatabaseGuard.
            var cadena = Lease.ConnectionString;
            TestDatabaseGuard.EnsureIsolated(cadena);

            // Esquema propio: exige que la base NO existiera y la crea desde
            // las migraciones del proyecto web.
            await Lease.PrepareAsync();

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MiCaminoBJJWeb"] = cadena
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection(); // emisión real de cookies en pruebas (como en la app)
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

            // Verificación de la conexión EFECTIVA del contexto de la app.
            using var alcanceVerificacion = Provider.CreateScope();
            var db = alcanceVerificacion.ServiceProvider.GetRequiredService<WebTfDbContext>();
            TestDatabaseGuard.EnsureIsolated(db, Lease.DatabaseName);
        }

        public async Task DisposeAsync()
        {
            // Alcances de las pruebas primero: sus contextos no retienen entidades.
            foreach (var alcancePrueba in _alcances)
                alcancePrueba.Dispose();

            await Provider.DisposeAsync();

            // Limpieza limitada al recurso creado por esta ejecución.
            await Lease.DropAsync();
        }

        /// <summary>
        /// Compatibilidad con las pruebas: los emails son únicos (guid) y la
        /// limpieza es la eliminación de la base aislada completa al cierre, por
        /// lo que NO se borran filas sueltas ni se depende del estado de otras
        /// cuentas. El método solo documenta la intención.
        /// </summary>
        public void RegistrarEmail(string email) => _ = email;
    }

    private readonly Fixture _fixture;

    public CuentaRegistroTests(Fixture fixture) => _fixture = fixture;

    /// <summary>Crea un controller con contexto HTTP real por prueba.</summary>
    private (CuentaController Controller, HttpContext Http) CrearController()
    {
        var alcance = _fixture.CrearAlcance();
        var sp = alcance.ServiceProvider;
        var http = new DefaultHttpContext { RequestServices = sp };
        _fixture.Accesor.HttpContext = http;

        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        // ActivatorUtilities: resuelve TODAS las dependencias del constructor
        // desde el contenedor (evita acoplar la prueba a la firma exacta).
        var controller = ActivatorUtilities.CreateInstance<CuentaController>(sp);

        // Patrón de Etapa 3: ControllerContext por inicializador (evita la
        // restricción de ControllerActionDescriptor del ctor con ActionContext).
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new VacioTempDataProvider());
        controller.Url = new UrlHelper(actionContext);

        return (controller, http);
    }

    private static string EmailUnico() => $"prueba-tfi-{Guid.NewGuid():N}@example.com";

    /// <summary>Replica la validación DataAnnotations de MVC en invocaciones directas.</summary>
    private static void ValidarEnModelState(RegistroViewModel model, Controller sut)
    {
        var resultados = new List<ValidationResult>();
        var contexto = new ValidationContext(model);
        if (Validator.TryValidateObject(model, contexto, resultados, validateAllProperties: true))
            return;

        foreach (var error in resultados)
        {
            foreach (var member in error.MemberNames)
                sut.ModelState.AddModelError(member, error.ErrorMessage!);
            if (!error.MemberNames.Any())
                sut.ModelState.AddModelError(string.Empty, error.ErrorMessage!);
        }
    }

    private static RegistroViewModel ModeloValido(string email) => new()
    {
        Nombre = "Prueba TFI",
        Email = email,
        Cinturon = "Blanco",
        Password = "Abcdef1!",
        ConfirmarPassword = "Abcdef1!"
    };

    // ── Registro válido ───────────────────────────────────────────────────

    [Fact]
    public async Task Registro_Valido_CreaIdentity_PerfilVinculado_YRolPracticante()
    {
        var email = EmailUnico();
        _fixture.RegistrarEmail(email);
        var (controller, _) = CrearController();
        var model = ModeloValido(email);
        ValidarEnModelState(model, controller);

        var resultado = await controller.Registro(model);

        // Redirige al login con mensaje de éxito.
        var redirect = resultado.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(CuentaController.Login));
        controller.TempData["Exito"].Should().NotBeNull();

        var sp = _fixture.Provider;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<WebTfDbContext>();

        var identity = await userManager.FindByEmailAsync(email);
        identity.Should().NotBeNull("debe existir el usuario Identity");
        identity!.DomainUserId.Should().NotBeNull("debe estar vinculado al perfil de dominio");

        var perfil = await db.Users.SingleAsync(u => u.Email == email);
        perfil.Id.Should().Be(identity.DomainUserId, "DomainUserId apunta al perfil recién creado");
        perfil.Name.Should().Be("Prueba TFI");
        perfil.Role.Should().Be(UserRole.Practicante, "el registro asigna EXCLUSIVAMENTE Practicante");
        perfil.BeltRank.Should().Be(model.Cinturon, "persiste el cinturón elegido en el registro");
        perfil.PasswordHash.Should().BeEmpty("las credenciales viven en AspNetUsers; no se duplican en el dominio");
        perfil.PasswordSalt.Should().BeEmpty();

        (await userManager.IsInRoleAsync(identity, "Practicante")).Should().BeTrue();
        (await userManager.IsInRoleAsync(identity, "Administrador")).Should().BeFalse("nunca se crea un admin desde el registro");

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        (await roleManager.RoleExistsAsync("Practicante")).Should().BeTrue("seed idempotente del rol");
        (await roleManager.RoleExistsAsync("Administrador")).Should().BeFalse("el rol admin no se crea en este flujo");
    }

    // ── Cinturón obligatorio: dato de perfil, nunca de rol ────────────────

    [Fact]
    public async Task Registro_SinCinturon_Rechazado_NoCreaNada()
    {
        var email = EmailUnico();
        _fixture.RegistrarEmail(email);
        var (controller, _) = CrearController();

        var model = new RegistroViewModel
        {
            Nombre = "Prueba TFI",
            Email = email,
            Cinturon = string.Empty, // obligatorio: queda vacío
            Password = "Abcdef1!",
            ConfirmarPassword = "Abcdef1!"
        };
        ValidarEnModelState(model, controller);

        var resultado = await controller.Registro(model);

        resultado.Should().BeOfType<ViewResult>();
        controller.ModelState.IsValid.Should().BeFalse();
        controller.ModelState[nameof(RegistroViewModel.Cinturon)]!.Errors.Should().NotBeEmpty(
            "el cinturón es un campo obligatorio del registro");

        var sp = _fixture.Provider;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.FindByEmailAsync(email)).Should().BeNull("sin cinturón no se crea la cuenta");
        var db = sp.GetRequiredService<WebTfDbContext>();
        (await db.Users.AnyAsync(u => u.Email == email)).Should().BeFalse("ni el perfil de dominio");
    }

    [Theory]
    [InlineData("Verde")]
    [InlineData("Blanco y Negro")]
    public async Task Registro_ConCinturonFueraDeLaLista_Rechazado_NoCreaNada(string cinturon)
    {
        var email = EmailUnico();
        _fixture.RegistrarEmail(email);
        var (controller, _) = CrearController();

        var model = ModeloValido(email);
        model.Cinturon = cinturon;
        ValidarEnModelState(model, controller);

        var resultado = await controller.Registro(model);

        resultado.Should().BeOfType<ViewResult>();
        controller.ModelState.IsValid.Should().BeFalse();
        controller.ModelState[nameof(RegistroViewModel.Cinturon)]!.Errors.Should().NotBeEmpty(
            "solo se aceptan los cinturones del dominio");

        var sp = _fixture.Provider;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.FindByEmailAsync(email)).Should().BeNull();
        var db = sp.GetRequiredService<WebTfDbContext>();
        (await db.Users.AnyAsync(u => u.Email == email)).Should().BeFalse();
    }

    [Fact]
    public async Task Registro_ConCinturonElegido_PersisteElCinturon_SinTocarElRol()
    {
        var email = EmailUnico();
        _fixture.RegistrarEmail(email);
        var (controller, _) = CrearController();

        var model = ModeloValido(email);
        model.Cinturon = "Violeta";
        ValidarEnModelState(model, controller);

        (await controller.Registro(model)).Should().BeOfType<RedirectToActionResult>();

        var db = _fixture.Provider.GetRequiredService<WebTfDbContext>();
        var perfil = await db.Users.SingleAsync(u => u.Email == email);
        perfil.BeltRank.Should().Be("Violeta", "el cinturón elegido se persiste en el perfil");
        perfil.Role.Should().Be(UserRole.Practicante,
            "el cinturón es un dato de perfil y NUNCA cambia el rol asignado en el servidor");

        var userManager = _fixture.Provider.GetRequiredService<UserManager<ApplicationUser>>();
        var identity = await userManager.FindByEmailAsync(email);
        (await userManager.IsInRoleAsync(identity!, "Practicante")).Should().BeTrue();
        (await userManager.IsInRoleAsync(identity!, "Administrador")).Should().BeFalse();
    }

    // ── Duplicado ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Registro_EmailDuplicado_Rechazado_SinSegundaCuenta()
    {
        var email = EmailUnico();
        _fixture.RegistrarEmail(email);
        var (controller, _) = CrearController();

        var primero = ModeloValido(email);
        ValidarEnModelState(primero, controller);
        (await controller.Registro(primero)).Should().BeOfType<RedirectToActionResult>();

        // Segundo intento con el mismo email (ModelState limpio).
        var duplicado = ModeloValido(email);
        ValidarEnModelState(duplicado, controller);
        controller.ModelState.Clear();
        var (otro, _) = CrearController();
        ValidarEnModelState(duplicado, otro);
        var resultado = await otro.Registro(duplicado);

        resultado.Should().BeOfType<ViewResult>();
        otro.ModelState[nameof(RegistroViewModel.Email)]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("Ya existe una cuenta con ese email.");

        var userManager = _fixture.Provider.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.FindByEmailAsync(email)).Should().NotBeNull("la cuenta original sigue existiendo");

        var db = _fixture.Provider.GetRequiredService<WebTfDbContext>();
        (await db.Users.CountAsync(u => u.Email == email)).Should().Be(1, "sin perfiles duplicados");
    }

    // ── Validaciones ──────────────────────────────────────────────────────

    [Fact]
    public async Task Registro_ContrasenaInvalida_NoCreaNada()
    {
        var email = EmailUnico();
        _fixture.RegistrarEmail(email); // por si acaso (no debería crearse nada)
        var (controller, _) = CrearController();

        var model = new RegistroViewModel
        {
            Nombre = "Prueba TFI",
            Email = email,
            Cinturon = "Blanco",
            Password = "123", // no cumple la política
            ConfirmarPassword = "123"
        };
        ValidarEnModelState(model, controller);

        var resultado = await controller.Registro(model);

        resultado.Should().BeOfType<ViewResult>();
        controller.ModelState.IsValid.Should().BeFalse();

        var sp = _fixture.Provider;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.FindByEmailAsync(email)).Should().BeNull("una contraseña inválida no crea usuarios");
        var db = sp.GetRequiredService<WebTfDbContext>();
        (await db.Users.AnyAsync(u => u.Email == email)).Should().BeFalse("ni perfiles");
    }

    [Fact]
    public async Task Registro_ConfirmacionDistinta_RechazadaEnModelo()
    {
        var email = EmailUnico();
        _fixture.RegistrarEmail(email);
        var (controller, _) = CrearController();

        var model = new RegistroViewModel
        {
            Nombre = "Prueba TFI",
            Email = email,
            Cinturon = "Blanco",
            Password = "Abcdef1!",
            ConfirmarPassword = "Distinta1!"
        };
        ValidarEnModelState(model, controller);

        var resultado = await controller.Registro(model);

        resultado.Should().BeOfType<ViewResult>();
        controller.ModelState[nameof(RegistroViewModel.ConfirmarPassword)]!.Errors.Should().NotBeEmpty();

        var db = _fixture.Provider.GetRequiredService<WebTfDbContext>();
        (await db.Users.AnyAsync(u => u.Email == email)).Should().BeFalse();
    }

    // ── Roles: jamás desde el cliente ─────────────────────────────────────

    [Fact]
    public void RegistroViewModel_NoExpone_NingunCampoDeRol()
    {
        var propiedades = typeof(RegistroViewModel)
            .GetProperties()
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .ToList();

        propiedades.Should().NotContain(new[] { "Role", "Rol", "IsAdmin", "Administrador" },
            "el formulario no puede recibir ni vincular roles");
        propiedades.Should().Equal(new[] { "Nombre", "Email", "Cinturon", "Password", "ConfirmarPassword" },
            "solo los campos mínimos reales del registro (perfil + credenciales)");
    }

    // ── Rollback transaccional ────────────────────────────────────────────

    [Fact]
    public async Task Registro_SiFallaElPerfilDeDominio_RevierteElUsuarioIdentity()
    {
        var email = EmailUnico();
        _fixture.RegistrarEmail(email); // limpieza también del perfil preexistente

        // Punto de fallo natural DESPUÉS de crear Identity: la clave única
        // Users.Email impide insertar el segundo perfil con el mismo email.
        var db = _fixture.Provider.GetRequiredService<WebTfDbContext>();
        db.Users.Add(new MiCaminoBJJ.Core.Entities.User
        {
            Name = "Perfil previo",
            Email = email,
            PasswordHash = string.Empty,
            PasswordSalt = string.Empty,
            BeltRank = "Blanco",
            Role = UserRole.Practicante,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var (controller, _) = CrearController();
        var model = ModeloValido(email);
        ValidarEnModelState(model, controller);

        var resultado = await controller.Registro(model);

        // Falló el paso de perfil → mensaje genérico y REVERSIÓN COMPLETA.
        resultado.Should().BeOfType<ViewResult>();
        controller.TempData["Error"].Should().NotBeNull();

        var sp = _fixture.Provider;
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.FindByEmailAsync(email)).Should().BeNull(
            "el usuario Identity creado debe haberse revertido junto con el fallo del perfil");

        var perfiles = await db.Users.CountAsync(u => u.Email == email);
        perfiles.Should().Be(1, "solo queda el perfil previo, sin huérfanos ni duplicados");
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
