using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Web.Identity;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Services.Admin;
using MiCaminoBJJ.Web.Tests.Infrastructure;

namespace MiCaminoBJJ.Web.Tests.Security;

/// <summary>
/// Etapa 4B — infraestructura de Identity (sin migración aplicada; todo en memoria/DI):
/// - Opciones de Identity (password, lockout, cookie pública propia).
/// - Esquemas de autenticación SEPARADOS (admin "Cookies" vs "Identity.Application").
/// - Política AdminLocal atada al esquema admin y al rol Administrador.
/// - Defaults de autenticación fail-closed hacia el esquema admin.
/// - Modelo EF: ApplicationUser + DomainUserId (opcional, único, SetNull).
/// - Ausencia de usuarios/roles sembrados automáticamente (cero HasData).
///
/// AISLAMIENTO (corrección crítica): la conexión de esta clase apunta a una
/// base de PRUEBA AISLADA y ÚNICA POR EJECUCIÓN verificada por
/// <see cref="TestDatabaseGuard"/>; JAMÁS a la base principal. Las pruebas que
/// necesitan esquema usan <see cref="TestDatabaseLease"/>, que demuestra que la
/// base no existía antes de crearla y solo elimina su propio recurso.
/// </summary>
[Collection("SqlServerLocalDbSeguridad")]
public class IdentityInfrastructureTests
{
    private const string SufijoBase = "Identity";

    private static ServiceProvider ConstruirServicios(string cadena)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MiCaminoBJJWeb"] = cadena
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWebTfPersistence(configuration);
        services.AddWebIdentity();
        services.AddAdminAuth(); // cookie admin + política AdminLocal (mismo orden que Program.cs)
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Servicios de Identity con una base aislada de SOLO CONFIGURACIÓN: no se
    /// abre ninguna conexión (estas pruebas verifican opciones y metadatos).
    /// </summary>
    private static ServiceProvider ConstruirServicios()
    {
        var cadena = TestDatabaseGuard.BuildIsolated(SufijoBase);
        return ConstruirServicios(cadena);
    }

    /// <summary>
    /// Contexto EF sobre una base AISLADA cuya conexión se valida antes de
    /// devolverlo (nunca la principal). El modelo se construye en memoria: no
    /// se requiere que la base exista.
    /// </summary>
    private static WebTfDbContext CrearContexto()
    {
        var cadena = TestDatabaseGuard.BuildIsolated(SufijoBase);
        var options = new DbContextOptionsBuilder<WebTfDbContext>();
        WebSqlServerOptions.Configure(options, cadena);

        var db = new WebTfDbContext(options.Options);
        TestDatabaseGuard.EnsureIsolated(db);
        return db;
    }

    // ── Opciones de Identity ──────────────────────────────────────────────

    [Fact]
    public void Identity_Configura_Password_Lockout_Y_UsuariosUnicos()
    {
        using var servicios = ConstruirServicios();
        var opciones = servicios.GetRequiredService<IOptions<IdentityOptions>>().Value;

        opciones.Password.RequiredLength.Should().BeGreaterThanOrEqualTo(8);
        opciones.Password.RequireNonAlphanumeric.Should().BeTrue();
        opciones.Password.RequireDigit.Should().BeTrue();
        opciones.Password.RequireUppercase.Should().BeTrue();
        opciones.Password.RequireLowercase.Should().BeTrue();

        opciones.Lockout.MaxFailedAccessAttempts.Should().Be(5);
        opciones.Lockout.DefaultLockoutTimeSpan.Should().Be(TimeSpan.FromMinutes(5));
        opciones.Lockout.AllowedForNewUsers.Should().BeTrue();

        opciones.User.RequireUniqueEmail.Should().BeTrue();
        opciones.SignIn.RequireConfirmedEmail.Should().BeFalse(
            "el envío de correo no está habilitado aún: la confirmación queda preparada, no forzada");
    }

    [Fact]
    public void Cookies_PublicaYAdmin_SonDistintas_YProtegidas()
    {
        using var servicios = ConstruirServicios();
        var cookies = servicios.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();

        var publica = cookies.Get(IdentityConstants.ApplicationScheme);
        var admin = cookies.Get(AdminPolicies.Scheme);

        publica.Cookie.Name.Should().Be(IdentitySetup.AppCookieName, "MiCaminoBJJ.Cuenta");
        publica.Cookie.Name.Should().NotBe(admin.Cookie.Name, "sin duplicación de nombres de cookie");
        publica.Cookie.HttpOnly.Should().BeTrue();
        publica.Cookie.SameSite.Should().Be(SameSiteMode.Lax);
        publica.Cookie.SecurePolicy.Should().Be(CookieSecurePolicy.SameAsRequest);
        publica.SlidingExpiration.Should().BeTrue("renovación controlada por actividad");
        publica.ExpireTimeSpan.Should().Be(TimeSpan.FromHours(8));
        publica.LoginPath.Value.Should().Be(IdentitySetup.LoginPath);

        admin.Cookie.Name.Should().Be("MiCaminoBJJ.Admin");
        admin.Cookie.HttpOnly.Should().BeTrue();
        admin.Cookie.SameSite.Should().Be(SameSiteMode.Lax);
    }

    // ── Esquemas separados ────────────────────────────────────────────────

    [Fact]
    public async Task EsquemasDeAutenticacion_Registrados_YSeparados()
    {
        using var servicios = ConstruirServicios();
        var proveedor = servicios.GetRequiredService<IAuthenticationSchemeProvider>();

        var nombres = (await proveedor.GetAllSchemesAsync()).Select(s => s.Name).ToList();

        nombres.Should().Contain(AdminPolicies.Scheme, "esquema admin actual (Cookies)");
        nombres.Should().Contain(IdentityConstants.ApplicationScheme, "esquema de Identity");
        nombres.Should().OnlyHaveUniqueItems("no debe duplicarse el registro de esquemas");

        var admin = await proveedor.GetSchemeAsync(AdminPolicies.Scheme);
        var identity = await proveedor.GetSchemeAsync(IdentityConstants.ApplicationScheme);
        admin.Should().NotBeNull("el esquema admin (Cookies) debe estar registrado");
        identity.Should().NotBeNull("el esquema de Identity debe estar registrado");
        admin!.Name.Should().NotBe(identity!.Name, "los esquemas deben ser distintos");
        admin.HandlerType.Should().Be(identity.HandlerType,
            "ambos son cookies, pero cada uno conserva su PROPIA configuración (CookieName distinto verificado arriba)");
    }

    [Fact]
    public void PoliticaAdminLocal_AtaEsquemaAdmin_Y_RolAdministrador()
    {
        using var servicios = ConstruirServicios();
        var politicas = servicios.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        var politica = politicas.GetPolicy(AdminPolicies.AdminLocal);

        politica.Should().NotBeNull("la política AdminLocal debe estar registrada");
        politica!.AuthenticationSchemes.Should().ContainSingle(
                "SOLO el esquema admin autoriza el CRUD: una cookie de Identity queda fuera por construcción")
            .Which.Should().Be(AdminPolicies.Scheme);
        politica.AuthenticationSchemes.Should().NotContain(IdentityConstants.ApplicationScheme);

        politica.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().Should().NotBeEmpty();

        var roles = politica.Requirements.OfType<RolesAuthorizationRequirement>().Should().ContainSingle().Subject;
        roles.AllowedRoles.Should().Contain(DevAdminCredential.RoleClaim,
            "la cookie admin actual ya emite este rol desde su emisión (DevAdminSession)");
    }

    [Fact]
    public void DefaultsDeAutenticacion_SonLosEstandarDeIdentity()
    {
        using var servicios = ConstruirServicios();
        var opciones = servicios.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        // Etapa 4C.1: sin PostConfigure manual ni middleware de fallback, los
        // defaults quedan en el esquema ESTÁNDAR de Identity. El aislamiento
        // del CRUD NO depende de los defaults: vive en la política AdminLocal,
        // anclada al esquema "Cookies" (probada en esta misma clase y en el
        // pipeline HTTP de SeguridadPipelineTests).
        opciones.DefaultAuthenticateScheme.Should().Be(IdentityConstants.ApplicationScheme);
        opciones.DefaultChallengeScheme.Should().Be(IdentityConstants.ApplicationScheme);
    }

    [Fact]
    public void PoliticaPracticanteAutenticado_AtaEsquemaIdentity_YRolPracticante()
    {
        using var servicios = ConstruirServicios();
        var politicas = servicios.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        var politica = politicas.GetPolicy(IdentitySetup.PoliticaPracticante);

        politica.Should().NotBeNull("la política PracticanteAutenticado debe estar registrada");
        politica!.AuthenticationSchemes.Should().Equal(new[] { IdentitySetup.AuthSchemeName });
        var roles = politica.Requirements.OfType<RolesAuthorizationRequirement>().Should().ContainSingle().Subject;
        roles.AllowedRoles.Should().Contain(IdentitySetup.RolPracticante);
    }

    [Fact]
    public void AdminTecnicas_UsaLaPoliticaDeAdministrador_NuncaAuthorizeDesnudo()
    {
        var authorize = typeof(MiCaminoBJJ.Web.Controllers.AdminTecnicasController)
            .GetCustomAttribute<AuthorizeAttribute>();

        authorize.Should().NotBeNull();
        authorize!.Policy.Should().Be(AdminPolicies.AdministradorContenido,
            "el CRUD debe usar la política explícita (nunca [Authorize] desnudo)");
        authorize.Policy.Should().NotBe(AdminPolicies.AdminLocal,
            "Etapa 5: el panel acepta TAMBIÉN una cuenta Identity con rol Administrador");
    }

    [Fact]
    public void PoliticaAdministradorContenido_AceptaIdentity_YCookieAdmin_ConRolAdministrador()
    {
        using var servicios = ConstruirServicios();
        var politicas = servicios.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        var politica = politicas.GetPolicy(AdminPolicies.AdministradorContenido);

        politica.Should().NotBeNull();
        politica!.AuthenticationSchemes.Should().HaveCount(2);
        politica.AuthenticationSchemes[0].Should().Be(IdentityConstants.ApplicationScheme,
            "Identity primero");
        politica.AuthenticationSchemes[1].Should().Be(CookieAuthenticationDefaults.AuthenticationScheme,
            "la cookie de Development al final: el challenge multi-esquema termina en /AdminAcceso/Login");

        var roles = politica.Requirements.OfType<RolesAuthorizationRequirement>().Should().ContainSingle().Subject;
        roles.AllowedRoles.Should().ContainSingle(
                "ambos esquemas exigen el MISMO rol Administrador (sin escalada)")
            .Which.Should().Be(DevAdminCredential.RoleClaim);

        politica.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().Should().NotBeEmpty();
    }

    [Fact]
    public async Task DevAdminSession_EmiteElRolQueExigeLaPoliticaAdmin()
    {
        // La política exige rol Administrador: verificamos que la emisión real
        // de la cookie admin lo incluye (el prerrequisito de la política).
        var http = new DefaultHttpContext();
        var servicioAuth = new ServicioAutenticacionRegistro();
        http.RequestServices = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(servicioAuth)
            .BuildServiceProvider();

        await new DevAdminSession().SignInAsync(http, DevAdminCredential.DefaultUser);

        servicioAuth.Principal.Should().NotBeNull();
        servicioAuth.Principal!.IsInRole(DevAdminCredential.RoleClaim).Should().BeTrue();
        servicioAuth.Esquema.Should().Be(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    // ── Modelo EF: vínculo con el dominio ─────────────────────────────────

    [Fact]
    public void Modelo_ApplicationUser_VinculaDomainUserId_AlUsuarioDeDominio()
    {
        using var db = CrearContexto();

        var entidad = db.Model.FindEntityType(typeof(ApplicationUser));
        entidad.Should().NotBeNull();

        var propiedad = entidad!.FindProperty(nameof(ApplicationUser.DomainUserId));
        propiedad.Should().NotBeNull();
        propiedad!.IsNullable.Should().BeTrue("hasta crear el perfil en la Etapa 4E es opcional");

        var indice = entidad.GetIndexes()
            .Single(i => i.Properties.Any(p => p.Name == nameof(ApplicationUser.DomainUserId)));
        indice.IsUnique.Should().BeTrue("la asociación identidad↔dominio es 1:1");

        var fk = entidad.GetForeignKeys()
            .Single(f => f.Properties.Any(p => p.Name == nameof(ApplicationUser.DomainUserId)));
        fk.PrincipalEntityType.ClrType.Should().Be(typeof(User),
            "la FK apunta a la entidad de dominio User sin cambiar su clave int");
        fk.DeleteBehavior.Should().Be(DeleteBehavior.SetNull,
            "purgar un perfil NO borra la credencial; la credencial nunca borra el dominio");
        fk.IsRequired.Should().BeFalse();
    }

    [Fact]
    public void Modelo_NoSemilla_NiUsuariosOAdministradoresAutomaticos()
    {
        using var db = CrearContexto();

        // EF Core 10: los HasData viven en el modelo design-time (el runtime
        // lanza "use IDesignTimeModel" explícitamente).
        var modelo = db.GetService<IDesignTimeModel>().Model;

        foreach (var entidad in modelo.GetEntityTypes())
        {
            var semilla = entidad.GetSeedData();
            (semilla ?? Enumerable.Empty<IDictionary<string, object?>>()).Should().BeEmpty(
                $"{entidad.ClrType.Name} no puede declarar HasData: ni usuarios ni roles predeterminados");
        }
    }

    [Fact]
    public async Task BaseAislada_Nueva_NoTieneUsuariosDeDominio()
    {
        // CORREGIDO (auditoría): antes esta prueba exigía 0 usuarios en la base
        // PRINCIPAL — lo que inducía a borrar cuentas reales para "hacerla pasar".
        // Ahora usa una base AISLADA y ÚNICA de esta ejecución: el resultado es
        // determinista e independiente de los datos reales de la aplicación y
        // de cualquier prueba paralela.

        // Arrendamiento único: falla si la base ya existiera (propiedad exclusiva).
        var lease = TestDatabaseLease.Create("IdentityEsquemaNuevo");

        await lease.PrepareAsync();
        try
        {
            await using var db = lease.CreateContext();
            var nombre = TestDatabaseGuard.EnsureIsolated(db, lease.DatabaseName);

            nombre.Should().StartWith(TestDatabaseGuard.TestDatabasePrefix,
                "la prueba solo puede operar sobre una base de pruebas aislada");
            TestDatabaseGuard.EsBasePrincipal(nombre).Should().BeFalse();
            TestDatabaseGuard.EsBaseDelSistema(nombre).Should().BeFalse();

            // Base recién creada desde las migraciones: sin usuarios ni perfiles.
            (await db.Users.CountAsync()).Should().Be(0,
                "una base nueva de pruebas no tiene usuarios de dominio");
            (await db.Set<ApplicationUser>().CountAsync()).Should().Be(0,
                "ni usuarios de Identity");
            (await db.Techniques.CountAsync()).Should().Be(0,
                "ni técnicas: la base se crea vacía, sin depender del catálogo real");
        }
        finally
        {
            await lease.DropAsync();
        }
    }

    // ── Fakes ─────────────────────────────────────────────────────────────

    private sealed class ServicioAutenticacionRegistro : IAuthenticationService
    {
        public ClaimsPrincipal? Principal { get; private set; }
        public string? Esquema { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            Esquema = scheme;
            Principal = principal;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
    }
}
