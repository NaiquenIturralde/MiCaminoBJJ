using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
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
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Web.Controllers;
using MiCaminoBJJ.Web.Identity;
using MiCaminoBJJ.Web.Localization;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Services.Perfil;
using MiCaminoBJJ.Web.Tests.Infrastructure;
using MiCaminoBJJ.Web.ViewModels;

namespace MiCaminoBJJ.Web.Tests.Security;

/// <summary>
/// Etapa 4D-B — pruebas del comando `init-admin` contra una base DE PRUEBA
/// AISLADA y ÚNICA POR EJECUCIÓN (prefijo obligatorio
/// <see cref="TestDatabaseGuard.TestDatabasePrefix"/>): jamás toca la base
/// principal, no ejecuta migraciones sobre datos reales y su limpieza solo
/// puede afectar al recurso creado por esta ejecución.
/// La contraseña NUNCA se pasa por argumentos: se inyecta con lectores en
/// memoria (el camino de consola no se usa en pruebas).
/// </summary>
[Collection("SqlServerLocalDbSeguridad")]
public class AdminBootstrapTests : IClassFixture<AdminBootstrapTests.Fixture>
{
    public sealed class Fixture : IAsyncLifetime
    {
        /// <summary>Base aislada EXCLUSIVA de esta ejecución (nombre único).</summary>
        public TestDatabaseLease Lease { get; } = TestDatabaseLease.Create("InitAdmin");

        public string CadenaConexion => Lease.ConnectionString;

        /// <summary>Nombre exacto cuyo borrado está autorizado para esta ejecución.</summary>
        public string BasePrueba => Lease.DatabaseName;

        public ServiceProvider Provider { get; private set; } = null!;
        public IWebHostEnvironment EntornoDevelopment { get; } = new EntornoFalso("Development");
        public IWebHostEnvironment EntornoProduccion { get; } = new EntornoFalso("Production");

        public async Task InitializeAsync()
        {
            // Guarda ANTES de cualquier preparación: la base debe ser aislada.
            TestDatabaseGuard.EnsureIsolated(CadenaConexion);

            // Esquema propio: exige que la base NO existiera y la crea desde
            // las migraciones (sin EnsureCreated ni bases compartidas).
            await Lease.PrepareAsync();

            var configuracion = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MiCaminoBJJWeb"] = CadenaConexion
                })
                .Build();

            var servicios = new ServiceCollection();
            servicios.AddLogging();
            servicios.AddDataProtection();
            servicios.AddWebTfPersistence(configuracion);
            servicios.AddWebIdentity();
            servicios.AddAdminAuth();
            servicios.AddWebLocalization(); // localizador de textos (es/en/pt)
            servicios.AddScoped<IAuditService, WebAuditService>(); // igual que Program.cs

            // Servicio de la foto de perfil (misma implementación que la app):
            // la carpeta de almacenamiento es TEMPORAL (no escribe en el repo).
            servicios.AddOptions<PerfilFotoOpciones>().Configure(opciones =>
                opciones.Ruta = Path.Combine(Path.GetTempPath(), "MiCaminoBJJ.Web.Tests", Guid.NewGuid().ToString("N")));
            servicios.AddScoped<IPerfilFotoService, PerfilFotoService>();

            Provider = servicios.BuildServiceProvider();

            await LimpiarAsync();
        }

        /// <summary>
        /// Limpieza acotada a la base de ESTA ejecución: se valida la CONEXIÓN
        /// EFECTIVA y que su nombre sea exactamente el del arrendamiento. La base
        /// es nueva (se creó aquí), así que sus filas son solo artefactos de
        /// prueba; nunca hay datos reales que perder.
        /// </summary>
        public async Task LimpiarAsync()
        {
            using var alcance = Provider.CreateScope();
            var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();

            // Fase 3 — la limpieza JAMÁS corre sin demostrar el destino.
            TestDatabaseGuard.EnsureIsolated(db, BasePrueba);

            await db.Database.ExecuteSqlRawAsync("""
                DELETE FROM [AuditLogs];
                DELETE FROM [AspNetUserRoles];
                DELETE FROM [AspNetUserClaims];
                DELETE FROM [AspNetUserTokens];
                DELETE FROM [AspNetUserLogins];
                DELETE FROM [AspNetUsers];
                DELETE FROM [Users];
                """);
        }

        public async Task DisposeAsync()
        {
            // Fase 3: una limpieza fallida NO se silencia (podría indicar que
            // el destino no era el esperado); se registra y se propaga. La base
            // eliminada es SIEMPRE la de este arrendamiento.
            try
            {
                await Provider.DisposeAsync();
            }
            finally
            {
                await Lease.DropAsync();
            }
        }
    }

    private readonly Fixture _fixture;

    public AdminBootstrapTests(Fixture fixture) => _fixture = fixture;

    // ── Helpers ───────────────────────────────────────────────────────────

    private static AdminBootstrap.InsumosInitAdmin Insumos(string email, string password, string? repetida = null) =>
        new(() => email, () => password, () => repetida ?? password);

    /// <summary>Lectores que MARCAN si la herramienta llega a pedir datos.</summary>
    private static (AdminBootstrap.InsumosInitAdmin Insumos, Func<bool> FuePedido) InsumosConMarca()
    {
        var pedido = false;
        AdminBootstrap.InsumosInitAdmin insumos = new(
            () => { pedido = true; return null; },
            () => { pedido = true; return null; },
            () => { pedido = true; return null; });
        return (insumos, () => pedido);
    }

    private async Task<int> Ejecutar(AdminBootstrap.InsumosInitAdmin insumos, string[] args, IWebHostEnvironment? entorno = null, string? cadenaConexion = null)
    {
        // Un alcance POR ejecución: los servicios scoped (contexto, UserManager)
        // no se comparten entre ejecuciones concurrentes; se libera al salir.
        // Si no se indica otra conexión, se usa la base DE PRUEBA: por eso se
        // pasa baseEsperada con el nombre de esa base (la CLI no lo hace nunca).
        using var alcance = cadenaConexion is null
            ? _fixture.Provider.CreateScope()
            : CrearProveedor(cadenaConexion).CreateScope();

        return await AdminBootstrap.EjecutarAsync(
            alcance.ServiceProvider,
            entorno ?? _fixture.EntornoDevelopment,
            args,
            insumos,
            baseEsperada: cadenaConexion is null ? _fixture.BasePrueba : null);
    }

    private static ServiceProvider CrearProveedor(string cadenaConexion)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MiCaminoBJJWeb"] = cadenaConexion
            })
            .Build();

        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddDataProtection();
        servicios.AddWebTfPersistence(configuracion);
        servicios.AddWebIdentity();
        servicios.AddAdminAuth();
        servicios.AddWebLocalization(); // localizador de textos (es/en/pt)
        servicios.AddScoped<IAuditService, WebAuditService>(); // igual que Program.cs
        return servicios.BuildServiceProvider();
    }

    private static string EmailUnico(string prefijo) => $"init-{prefijo}-{Guid.NewGuid():N}@example.com";

    private const string PasswordValida = "Abcdef1!";

    private async Task<(string Email, string Id)> CrearAdministradorDirectoAsync(string email)
    {
        using var alcance = _fixture.Provider.CreateScope();
        var userManager = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var creacion = await userManager.CreateAsync(new ApplicationUser { UserName = email, Email = email }, PasswordValida);
        creacion.Succeeded.Should().BeTrue(string.Join("; ", creacion.Errors.Select(e => e.Description)));
        var user = await userManager.FindByEmailAsync(email);
        return (email, user!.Id);
    }

    // ── 1. Creación y vínculo de dominio ──────────────────────────────────

    [Fact]
    public async Task CreacionValida_CreaAdministrador_VinculadoAlDominio_YRol()
    {
        await _fixture.LimpiarAsync();
        var email = EmailUnico("creacion");

        var codigo = await Ejecutar(Insumos(email, PasswordValida), new[] { AdminBootstrap.Comando });

        codigo.Should().Be(AdminBootstrap.ExitOk);

        using var alcance = _fixture.Provider.CreateScope();
        var userManager = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = alcance.ServiceProvider.GetRequiredService<RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();

        var identity = await userManager.FindByEmailAsync(email);
        identity.Should().NotBeNull();
        identity!.DomainUserId.Should().NotBeNull("debe estar vinculado al perfil de dominio");

        var perfil = await db.Users.SingleAsync(u => u.Email == email);
        perfil.Id.Should().Be(identity.DomainUserId);
        perfil.Role.Should().Be(UserRole.Administrador, "el espejo de dominio refleja el rol real");
        perfil.BeltRank.Should().Be("Blanco");
        perfil.PasswordHash.Should().BeEmpty("las credenciales viven solo en AspNetUsers");

        (await userManager.IsInRoleAsync(identity, "Administrador")).Should().BeTrue();
        (await roleManager.RoleExistsAsync("Administrador")).Should().BeTrue();
    }

    // ── 2. Validación de contraseña ───────────────────────────────────────

    [Fact]
    public async Task PasswordDebil_EsRechazada_NoCreaNada()
    {
        await _fixture.LimpiarAsync();
        var email = EmailUnico("password");

        var codigo = await Ejecutar(Insumos(email, "123"), new[] { AdminBootstrap.Comando });

        codigo.Should().Be(AdminBootstrap.ExitEntrada);

        using var alcance = _fixture.Provider.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        (await db.Users.AnyAsync(u => u.Email == email)).Should().BeFalse();
        var userManager = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.FindByEmailAsync(email)).Should().BeNull();
    }

    [Fact]
    public async Task ConfirmacionDistinta_EsRechazada_NoCreaNada()
    {
        await _fixture.LimpiarAsync();
        var email = EmailUnico("confirmacion");

        var codigo = await Ejecutar(Insumos(email, PasswordValida, "Distinta1!"), new[] { AdminBootstrap.Comando });

        codigo.Should().Be(AdminBootstrap.ExitEntrada);

        using var alcance = _fixture.Provider.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        (await db.Users.AnyAsync(u => u.Email == email)).Should().BeFalse();
    }

    // ── 3. Email duplicado ────────────────────────────────────────────────

    [Fact]
    public async Task EmailDuplicado_EsRechazado()
    {
        await _fixture.LimpiarAsync();
        var email = EmailUnico("duplicado");
        await CrearAdministradorDirectoAsync(email);

        var codigo = await Ejecutar(Insumos(email, PasswordValida), new[] { AdminBootstrap.Comando });

        codigo.Should().Be(AdminBootstrap.ExitDuplicado);

        using var alcance = _fixture.Provider.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        var userManager = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.Users.CountAsync(u => u.Email == email)).Should().Be(1, "sin cuentas Identity duplicadas");
        (await db.Users.CountAsync(u => u.Email == email)).Should().Be(0, "el rechazo no creó ningún perfil de dominio");
    }

    // ── 4. Inicialización repetida ────────────────────────────────────────

    [Fact]
    public async Task InicializacionRepetida_EsRechazada()
    {
        await _fixture.LimpiarAsync();

        var primero = EmailUnico("repetida-1");
        (await Ejecutar(Insumos(primero, PasswordValida), new[] { AdminBootstrap.Comando }))
            .Should().Be(AdminBootstrap.ExitOk);

        var segundo = EmailUnico("repetida-2");
        var codigo = await Ejecutar(Insumos(segundo, PasswordValida), new[] { AdminBootstrap.Comando });

        codigo.Should().Be(AdminBootstrap.ExitYaInicializado);

        using var alcance = _fixture.Provider.CreateScope();
        var roleManager = alcance.ServiceProvider.GetRequiredService<RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();
        var userManager = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admins = await userManager.GetUsersInRoleAsync("Administrador");
        admins.Should().HaveCount(1, "no debe crearse un segundo administrador");
        (await roleManager.RoleExistsAsync("Administrador")).Should().BeTrue();
    }

    // ── 5. Concurrencia ───────────────────────────────────────────────────

    [Fact]
    public async Task EjecucionesSimultaneas_SoloUnaSucceed()
    {
        await _fixture.LimpiarAsync();

        var emailA = EmailUnico("conc-A");
        var emailB = EmailUnico("conc-B");

        var tareaA = Ejecutar(Insumos(emailA, PasswordValida), new[] { AdminBootstrap.Comando });
        var tareaB = Ejecutar(Insumos(emailB, PasswordValida), new[] { AdminBootstrap.Comando });

        var resultados = await Task.WhenAll(tareaA, tareaB);

        resultados.Count(c => c == AdminBootstrap.ExitOk).Should().Be(1, "exactamente una ejecución crea el administrador");
        resultados.Should().Contain(c =>
            c == AdminBootstrap.ExitConcurrencia || c == AdminBootstrap.ExitYaInicializado,
            "la otra debe rechazarse por candado o por inicialización ya realizada");

        using var alcance = _fixture.Provider.CreateScope();
        var userManager = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.GetUsersInRoleAsync("Administrador")).Should().HaveCount(1);
    }

    // ── 6. Rollback ante errores ──────────────────────────────────────────

    [Fact]
    public async Task PerfilDeDominioDuplicado_RevierteTodo()
    {
        await _fixture.LimpiarAsync();
        var email = EmailUnico("rollback");

        // Punto de fallo natural DESPUÉS de crear Identity: la clave única
        // Users.Email impide insertar el segundo perfil con el mismo email.
        using (var alcance = _fixture.Provider.CreateScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
            db.Users.Add(new User
            {
                Name = "Perfil previo",
                Email = email,
                PasswordHash = string.Empty,
                PasswordSalt = string.Empty,
                BeltRank = "Blanco",
                Role = UserRole.Administrador,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var codigo = await Ejecutar(Insumos(email, PasswordValida), new[] { AdminBootstrap.Comando });

        codigo.Should().Be(AdminBootstrap.ExitInterno, "el fallo del perfil revierte toda la operación");

        using var alcance2 = _fixture.Provider.CreateScope();
        var db2 = alcance2.ServiceProvider.GetRequiredService<WebTfDbContext>();
        var userManager = alcance2.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        (await userManager.FindByEmailAsync(email)).Should().BeNull(
            "el usuario Identity creado debe haberse revertido");
        (await db2.Users.CountAsync(u => u.Email == email)).Should().Be(1, "solo queda el perfil previo");
        var auditorias = await db2.AuditLogs.CountAsync(a =>
            a.EntityType == nameof(User) && a.Details != null && a.Details.Contains(email));
        auditorias.Should().Be(0, "sin auditoría de una operación revertida");
    }

    // ── 7. Auditoría sin secretos ─────────────────────────────────────────

    [Fact]
    public async Task Auditoria_RegistraOperacion_SinContraseñasNiTokens()
    {
        await _fixture.LimpiarAsync();
        var email = EmailUnico("auditoria");
        const string passwordConMarca = "Zz9!Marca-Secreta-4DB";

        var codigo = await Ejecutar(Insumos(email, passwordConMarca), new[] { AdminBootstrap.Comando });
        codigo.Should().Be(AdminBootstrap.ExitOk);

        using var alcance = _fixture.Provider.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();

        var perfil = await db.Users.SingleAsync(u => u.Email == email);
        var auditoria = await db.AuditLogs.SingleAsync(a =>
            a.EntityType == nameof(User) && a.EntityId == perfil.Id);

        auditoria.Action.Should().Be(AuditAction.Create);
        auditoria.Result.Should().Be(AuditResult.Success);
        auditoria.UserId.Should().BeNull("sin usuario de dominio previo al vínculo");
        auditoria.Details.Should().Contain("init-admin-local");
        auditoria.Details.Should().Contain("InitAdmin");
        auditoria.Details.Should().Contain(email);
        auditoria.Details.Should().NotContain(passwordConMarca, "la contraseña jamás se registra");
        auditoria.Details.Should().NotContain("token", "tampoco tokens");
        auditoria.ErrorMessage.Should().BeNull();
    }

    // ── 8. Rechazo fuera de Development ───────────────────────────────────

    [Fact]
    public async Task FueraDeDevelopment_EsRechazado_SinPedirDatos()
    {
        await _fixture.LimpiarAsync();
        var (insumos, fuePedido) = InsumosConMarca();

        var codigo = await Ejecutar(insumos, new[] { AdminBootstrap.Comando }, entorno: _fixture.EntornoProduccion);

        codigo.Should().Be(AdminBootstrap.ExitEntorno);
        fuePedido().Should().BeFalse("ni email ni contraseña se piden fuera de Development");
    }

    // ── 9. Protección de la base autorizada ───────────────────────────────

    [Fact]
    public async Task BaseNoAutorizada_EsRechazada_SinPedirDatos()
    {
        // Conexión apuntando a 'master' con el nombre esperado por defecto
        // (la base autorizada): rechazo ANTES de cualquier interacción.
        var (insumos, fuePedido) = InsumosConMarca();

        var codigo = await Ejecutar(
            insumos,
            new[] { AdminBootstrap.Comando },
            cadenaConexion: "Server=(localdb)\\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True");

        codigo.Should().Be(AdminBootstrap.ExitBase);
        fuePedido().Should().BeFalse("no se pide nada cuando la base no es la autorizada");
    }

    [Fact]
    public async Task ServidorNoAutorizado_EsRechazado_SinPedirDatos()
    {
        await _fixture.LimpiarAsync();
        var (insumos, fuePedido) = InsumosConMarca();

        var codigo = await Ejecutar(
            insumos,
            new[] { AdminBootstrap.Comando },
            cadenaConexion: "Server=localhost;Database=MiCaminoBJJ_Web_TFI_2026;Trusted_Connection=True;TrustServerCertificate=True");

        codigo.Should().Be(AdminBootstrap.ExitBase);
        fuePedido().Should().BeFalse("valida servidor antes de pedir credenciales");
    }

    [Fact]
    public async Task ArgumentosConPassword_SonRechazados_SinPedirDatos()
    {
        await _fixture.LimpiarAsync();
        var (insumos, fuePedido) = InsumosConMarca();

        var codigo = await Ejecutar(insumos, new[] { AdminBootstrap.Comando, "--password=NoPasaPorAqui!" });

        codigo.Should().Be(AdminBootstrap.ExitUso, "los secretos jamás se aceptan por argumentos");
        fuePedido().Should().BeFalse();

        var ayuda = await Ejecutar(insumos, new[] { AdminBootstrap.Comando, "--help" });
        ayuda.Should().Be(AdminBootstrap.ExitUso);
        fuePedido().Should().BeFalse();
    }

    // ── 10. Registro público limitado al rol Practicante ──────────────────

    [Fact]
    public async Task RegistroPublico_SigueAsignandoSoloPracticante()
    {
        await _fixture.LimpiarAsync();
        var email = EmailUnico("registro");

        using var alcance = _fixture.Provider.CreateScope();
        var sp = alcance.ServiceProvider;

        var http = new DefaultHttpContext { RequestServices = sp };
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        // ActivatorUtilities: resuelve TODAS las dependencias del constructor
        // desde el contenedor (evita acoplar la prueba a la firma exacta).
        var controller = ActivatorUtilities.CreateInstance<CuentaController>(sp);
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new VacioTempDataProvider());
        controller.Url = new UrlHelper(actionContext);

        var model = new RegistroViewModel
        {
            Nombre = "Registro Aislado",
            Email = email,
            Cinturon = "Blanco",
            Password = PasswordValida,
            ConfirmarPassword = PasswordValida
        };

        var resultados = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            model, new System.ComponentModel.DataAnnotations.ValidationContext(model), resultados, true);
        foreach (var error in resultados)
            controller.ModelState.AddModelError(string.Empty, error.ErrorMessage!);

        var resultado = await controller.Registro(model);
        resultado.Should().BeOfType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>();

        var identity = await sp.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
        identity.Should().NotBeNull();
        (await sp.GetRequiredService<UserManager<ApplicationUser>>().IsInRoleAsync(identity!, "Practicante")).Should().BeTrue();
        (await sp.GetRequiredService<UserManager<ApplicationUser>>().IsInRoleAsync(identity!, "Administrador")).Should().BeFalse(
            "el registro jamás otorga administración");

        var db = sp.GetRequiredService<WebTfDbContext>();
        (await db.Users.SingleAsync(u => u.Email == email)).Role.Should().Be(UserRole.Practicante);

        var administradores = await sp.GetRequiredService<UserManager<ApplicationUser>>().GetUsersInRoleAsync("Administrador");
        administradores.Should().BeEmpty("esta prueba no debe crear administradores");
    }

    // ── Fakes ─────────────────────────────────────────────────────────────

    private sealed class EntornoFalso : IWebHostEnvironment
    {
        public EntornoFalso(string environmentName) => EnvironmentName = environmentName;

        public string ApplicationName { get; set; } = "MiCaminoBJJ.Web";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "C:\\";
        public string EnvironmentName { get; set; }
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "C:\\";
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
