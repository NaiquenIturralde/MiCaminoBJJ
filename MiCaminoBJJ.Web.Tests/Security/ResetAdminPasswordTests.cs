using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Web.Identity;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Tests.Infrastructure;

namespace MiCaminoBJJ.Web.Tests.Security;

/// <summary>
/// Etapa 4D-C — pruebas del comando <c>reset-admin-password</c> contra una base
/// de PRUEBA AISLADA y ÚNICA POR EJECUCIÓN (prefijo obligatorio
/// <see cref="TestDatabaseGuard.TestDatabasePrefix"/>): jamás toca la base
/// principal y su limpieza solo puede afectar al recurso creado por esta
/// ejecución.
///
/// La contraseña NUNCA viaja por argumentos: se inyecta con lectores en
/// memoria (el camino de consola no se usa en pruebas). El perfil de dominio
/// esperado se parametriza para que la prueba no dependa del Id 339 real.
/// </summary>
[Collection("SqlServerLocalDbSeguridad")]
public class ResetAdminPasswordTests : IClassFixture<ResetAdminPasswordTests.Fixture>
{
    public sealed class Fixture : IAsyncLifetime
    {
        /// <summary>Base aislada EXCLUSIVA de esta ejecución (nombre único).</summary>
        public TestDatabaseLease Lease { get; } = TestDatabaseLease.Create("ResetAdmin");

        public string CadenaConexion => Lease.ConnectionString;

        /// <summary>Nombre exacto cuyo borrado está autorizado para esta ejecución.</summary>
        public string BasePrueba => Lease.DatabaseName;

        public ServiceProvider Provider { get; private set; } = null!;
        public IWebHostEnvironment EntornoDevelopment { get; } = new EntornoFalso("Development");
        public IWebHostEnvironment EntornoProduccion { get; } = new EntornoFalso("Production");

        /// <summary>Carpeta de respaldos de las pruebas (temporal, propia de la corrida).</summary>
        public string CarpetaRespaldo { get; } =
            Path.Combine(Path.GetTempPath(), "mccj_resetadmin_" + Guid.NewGuid().ToString("N")[..8]);

        public async Task InitializeAsync()
        {
            // Guarda ANTES de cualquier preparación: la base debe ser aislada.
            TestDatabaseGuard.EnsureIsolated(CadenaConexion);

            // Esquema propio desde las migraciones (sin EnsureCreated).
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
            servicios.AddScoped<IAuditService, WebAuditService>(); // igual que Program.cs

            Provider = servicios.BuildServiceProvider();

            await LimpiarAsync();
        }

        /// <summary>
        /// Limpieza acotada a la base de ESTA ejecución: se valida la CONEXIÓN
        /// EFECTIVA y que su nombre sea exactamente el del arrendamiento.
        /// </summary>
        public async Task LimpiarAsync()
        {
            using var alcance = Provider.CreateScope();
            var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();

            // La limpieza JAMÁS corre sin demostrar el destino.
            TestDatabaseGuard.EnsureIsolated(db, BasePrueba);

            await db.Database.ExecuteSqlRawAsync("""
                DELETE FROM [AuditLogs];
                DELETE FROM [AspNetUserRoles];
                DELETE FROM [AspNetUserClaims];
                DELETE FROM [AspNetUserTokens];
                DELETE FROM [AspNetUserLogins];
                DELETE FROM [AspNetUsers];
                DELETE FROM [AspNetRoleClaims];
                DELETE FROM [AspNetRoles];
                DELETE FROM [Users];
                """);
        }

        public async Task DisposeAsync()
        {
            try
            {
                await Provider.DisposeAsync();
            }
            finally
            {
                if (Directory.Exists(CarpetaRespaldo))
                {
                    try { Directory.Delete(CarpetaRespaldo, recursive: true); }
                    catch (IOException) { /* residuo temporal: no afecta el resultado */ }
                }

                await Lease.DropAsync();
            }
        }
    }

    private readonly Fixture _fixture;

    public ResetAdminPasswordTests(Fixture fixture) => _fixture = fixture;

    private const string PasswordOriginal = "Original1!";
    private const string PasswordNueva = "Restablecida2#";
    private const string RolAdmin = "Administrador";
    private const string RolPracticante = "Practicante";

    // ── Helpers ───────────────────────────────────────────────────────────

    private static ResetAdminPassword.Insumos Insumos(
        string email,
        string password,
        string? repetida = null,
        string? confirmacion = "CONFIRMAR")
    {
        return new ResetAdminPassword.Insumos(
            () => email,
            () => password,
            () => repetida ?? password,
            () => confirmacion);
    }

    /// <summary>Lectores que MARCAN si la herramienta llega a pedir datos.</summary>
    private static (ResetAdminPassword.Insumos Insumos, Func<bool> FuePedido) InsumosConMarca()
    {
        var pedido = false;
        ResetAdminPassword.Insumos insumos = new(
            () => { pedido = true; return null; },
            () => { pedido = true; return null; },
            () => { pedido = true; return null; },
            () => { pedido = true; return null; });
        return (insumos, () => pedido);
    }

    private async Task<int> Ejecutar(
        ResetAdminPassword.Insumos insumos,
        string[] args,
        ResetAdminPassword.Opciones? opciones = null,
        IWebHostEnvironment? entorno = null,
        string? cadenaConexion = null)
    {
        // Un alcance POR ejecución: los servicios scoped no se comparten.
        var proveedor = cadenaConexion is null ? _fixture.Provider : CrearProveedor(cadenaConexion);
        try
        {
            using var alcance = proveedor.CreateScope();
            return await ResetAdminPassword.EjecutarAsync(
                alcance.ServiceProvider,
                entorno ?? _fixture.EntornoDevelopment,
                args,
                insumos,
                opciones ?? OpcionesPrueba());
        }
        finally
        {
            if (cadenaConexion is not null)
                await proveedor.DisposeAsync();
        }
    }

    /// <summary>
    /// Opciones seguras para pruebas: base AISLADA, respaldo en carpeta
    /// temporal propia y el perfil de dominio que cada prueba haya creado.
    /// </summary>
    private ResetAdminPassword.Opciones OpcionesPrueba(int domainUserId = 0, bool respaldo = true) =>
        new(
            DomainUserIdEsperado: domainUserId,
            BaseEsperada: _fixture.BasePrueba,
            CarpetaRespaldo: _fixture.CarpetaRespaldo,
            EjecutarRespaldo: respaldo);

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
        servicios.AddScoped<IAuditService, WebAuditService>();
        return servicios.BuildServiceProvider();
    }

    private static string EmailUnico(string prefijo) =>
        $"reset-{prefijo}-{Guid.NewGuid():N}@example.com";

    /// <summary>
    /// Crea el escenario real: perfil de dominio + cuenta Identity vinculada +
    /// rol Administrador. Devuelve el <c>Users.Id</c> del perfil.
    /// </summary>
    private async Task<(string Email, int DomainUserId)> CrearAdministradorAsync(
        string? email = null, bool asignarRol = true, bool vincular = true)
    {
        email ??= EmailUnico("admin");

        using var alcance = _fixture.Provider.CreateScope();
        var sp = alcance.ServiceProvider;
        var db = sp.GetRequiredService<WebTfDbContext>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();

        var perfil = new User
        {
            Name = "Administrador de prueba",
            Email = email,
            PasswordHash = string.Empty,
            PasswordSalt = string.Empty,
            BeltRank = "Blanco",
            Role = UserRole.Administrador,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(perfil);
        await db.SaveChangesAsync();

        var identity = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DomainUserId = vincular ? perfil.Id : null
        };
        var creacion = await userManager.CreateAsync(identity, PasswordOriginal);
        creacion.Succeeded.Should().BeTrue(
            string.Join("; ", creacion.Errors.Select(e => e.Description)));

        // Se persiste el vínculo si el escenario lo pidió.
        if (vincular)
        {
            identity.DomainUserId = perfil.Id;
            var vinculo = await userManager.UpdateAsync(identity);
            vinculo.Succeeded.Should().BeTrue(string.Join("; ", vinculo.Errors.Select(e => e.Description)));
        }

        if (asignarRol)
        {
            if (!await roleManager.RoleExistsAsync(RolAdmin))
            {
                var creacionRol = await roleManager.CreateAsync(new IdentityRole(RolAdmin));
                creacionRol.Succeeded.Should().BeTrue(
                    string.Join("; ", creacionRol.Errors.Select(e => e.Description)));
            }

            var asignacion = await userManager.AddToRoleAsync(identity, RolAdmin);
            asignacion.Succeeded.Should().BeTrue(
                string.Join("; ", asignacion.Errors.Select(e => e.Description)));
        }

        return (email, perfil.Id);
    }

    // ── 1. Camino feliz ───────────────────────────────────────────────────

    [Fact]
    public async Task Restablecimiento_Valido_CambiaSoloLaContrasena_ConservaRolVinculoYPerfil()
    {
        await _fixture.LimpiarAsync();
        var (email, domainUserId) = await CrearAdministradorAsync();

        string stampAntes;
        using (var alcance = _fixture.Provider.CreateScope())
        {
            var um = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var antes = await um.FindByEmailAsync(email);
            stampAntes = await um.GetSecurityStampAsync(antes!);
        }

        var codigo = await Ejecutar(
            Insumos(email, PasswordNueva),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId));

        codigo.Should().Be(ResetAdminPassword.ExitOk);

        using var verificacion = _fixture.Provider.CreateScope();
        var userManager = verificacion.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = verificacion.ServiceProvider.GetRequiredService<WebTfDbContext>();

        var identity = await userManager.FindByEmailAsync(email);
        identity.Should().NotBeNull();

        // Contraseña: la nueva funciona y la vieja ya no.
        (await userManager.CheckPasswordAsync(identity!, PasswordNueva)).Should().BeTrue();
        (await userManager.CheckPasswordAsync(identity!, PasswordOriginal)).Should().BeFalse();

        // El SecurityStamp rotó → las cookies emitidas antes dejan de valer.
        var stampDespues = await userManager.GetSecurityStampAsync(identity!);
        stampDespues.Should().NotBe(stampAntes, "el reset rota el SecurityStamp e invalida sesiones viejas");

        // Rol y vínculo INTACTOS.
        (await userManager.IsInRoleAsync(identity!, RolAdmin)).Should().BeTrue(
            "el comando verifica el rol pero jamás lo modifica");
        (await userManager.IsInRoleAsync(identity!, RolPracticante)).Should().BeFalse();
        identity!.DomainUserId.Should().Be(domainUserId, "el vínculo 1:1 no se altera");

        // Perfil de dominio intacto (el comando no lo escribe).
        var perfil = await db.Users.AsNoTracking().SingleAsync(u => u.Id == domainUserId);
        perfil.Email.Should().Be(email);
        perfil.Role.Should().Be(UserRole.Administrador);
        perfil.PasswordHash.Should().BeEmpty("las credenciales viven solo en AspNetUsers");
        perfil.BeltRank.Should().Be("Blanco");

        // No se creó ningún usuario ni rol adicional.
        (await db.Users.CountAsync()).Should().Be(1);
        (await db.Set<ApplicationUser>().CountAsync()).Should().Be(1);

        // Se creó EXACTAMENTE el rol Administrador y la cuenta tiene un solo rol.
        var roles = await verificacion.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>().Roles.Select(r => r.Name).ToListAsync();
        roles.Should().BeEquivalentTo(new[] { RolAdmin });
        (await db.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>()
            .CountAsync()).Should().Be(1, "el comando no agrega ni quita asignaciones de rol");
    }

    [Fact]
    public async Task Restablecimiento_RegistraAuditoria_SinContrasenaNiToken()
    {
        await _fixture.LimpiarAsync();
        var (email, domainUserId) = await CrearAdministradorAsync();

        var codigo = await Ejecutar(
            Insumos(email, PasswordNueva),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId));

        codigo.Should().Be(ResetAdminPassword.ExitOk);

        using var alcance = _fixture.Provider.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();

        var auditoria = await db.AuditLogs.AsNoTracking().SingleAsync();
        auditoria.Action.Should().Be(AuditAction.Update);
        auditoria.EntityType.Should().Be(nameof(User));
        auditoria.EntityId.Should().Be(domainUserId);
        auditoria.Result.Should().Be(AuditResult.Success);
        auditoria.Details.Should().NotBeNull();

        // El detalle incluye lo verificable y NUNCA material sensible.
        auditoria.Details!.Should().Contain("ResetAdminPassword");
        auditoria.Details!.Should().Contain("reset-admin-password-local");
        auditoria.Details!.Should().Contain("RolAdministradorVerificado");
        auditoria.Details!.Should().NotContain(PasswordNueva);
        auditoria.Details!.Should().NotContain(PasswordOriginal);

        // El JSON es válido y no arrastra claves de credenciales.
        using var json = JsonDocument.Parse(auditoria.Details!);
        json.RootElement.EnumerateObject()
            .Select(p => p.Name.ToLowerInvariant())
            .Should().NotContain(n => n.Contains("password") || n.Contains("hash") || n.Contains("token"));
    }

    [Fact]
    public async Task Restablecimiento_CreaRespaldoVerificado_AntesDeEscribir()
    {
        await _fixture.LimpiarAsync();
        var (email, domainUserId) = await CrearAdministradorAsync();

        // La carpeta es compartida por la clase: se comparan los archivos
        // ANTES y DESPUÉS para aislar el respaldo de ESTA ejecución.
        Directory.CreateDirectory(_fixture.CarpetaRespaldo);
        var respaldosPrevios = Directory
            .GetFiles(_fixture.CarpetaRespaldo, "*_preResetPassword_*.bak")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var codigo = await Ejecutar(
            Insumos(email, PasswordNueva),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId, respaldo: true));

        codigo.Should().Be(ResetAdminPassword.ExitOk);

        var respaldosNuevos = Directory
            .GetFiles(_fixture.CarpetaRespaldo, "*_preResetPassword_*.bak")
            .Where(r => !respaldosPrevios.Contains(r))
            .ToList();

        respaldosNuevos.Should().HaveCount(1,
            "debe existir exactamente el respaldo previo de esta ejecución");
        new FileInfo(respaldosNuevos[0]).Length.Should().BeGreaterThan(0,
            "un archivo de respaldo vacío no cuenta como respaldo válido");

        // El respaldo se tomó ANTES de escribir: se verifica que el archivo
        // existe y que la operación luego se confirmó.
        using var alcance = _fixture.Provider.CreateScope();
        var um = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var identity = await um.FindByEmailAsync(email);
        (await um.CheckPasswordAsync(identity!, PasswordNueva)).Should().BeTrue();
    }

    [Fact]
    public async Task DesbloqueoDefensivo_DejaLaCuentaUtilizable()
    {
        await _fixture.LimpiarAsync();
        var (email, domainUserId) = await CrearAdministradorAsync();

        // Se fuerza un bloqueo vigente con intentos acumulados.
        using (var alcance = _fixture.Provider.CreateScope())
        {
            var um = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var identity = await um.FindByEmailAsync(email);
            await um.SetLockoutEndDateAsync(identity!, DateTimeOffset.UtcNow.AddMinutes(10));
            await um.ResetAccessFailedCountAsync(identity!);
            for (var i = 0; i < 3; i++)
                await um.AccessFailedAsync(identity!);
        }

        var codigo = await Ejecutar(
            Insumos(email, PasswordNueva),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId));

        codigo.Should().Be(ResetAdminPassword.ExitOk);

        using var verificacion = _fixture.Provider.CreateScope();
        var userManager = verificacion.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var cuenta = await userManager.FindByEmailAsync(email);

        (await userManager.IsLockedOutAsync(cuenta!)).Should().BeFalse(
            "una cuenta bloqueada no podría usar la contraseña nueva");
        (await userManager.GetAccessFailedCountAsync(cuenta!)).Should().Be(0);
    }

    // ── 2. Rechazos: contraseña ───────────────────────────────────────────

    [Fact]
    public async Task PasswordDebil_EsRechazada_NoCambiaLaContrasena()
    {
        await _fixture.LimpiarAsync();
        var (email, domainUserId) = await CrearAdministradorAsync();

        var codigo = await Ejecutar(
            Insumos(email, "corta1!"),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId));

        codigo.Should().Be(ResetAdminPassword.ExitEntrada);

        using var alcance = _fixture.Provider.CreateScope();
        var um = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var identity = await um.FindByEmailAsync(email);
        (await um.CheckPasswordAsync(identity!, PasswordOriginal)).Should().BeTrue(
            "un rechazo de política no debe haber tocado el hash");
        (await um.CheckPasswordAsync(identity!, "corta1!")).Should().BeFalse();
    }

    [Fact]
    public async Task PasswordSinCaracterEspecial_EsRechazada()
    {
        await _fixture.LimpiarAsync();
        var (email, domainUserId) = await CrearAdministradorAsync();

        var codigo = await Ejecutar(
            Insumos(email, "SinEspecial123"),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId));

        codigo.Should().Be(ResetAdminPassword.ExitEntrada,
            "la política configurada exige al menos un carácter no alfanumérico");
    }

    [Fact]
    public async Task ConfirmacionDistinta_EsRechazada_NoCambiaNada()
    {
        await _fixture.LimpiarAsync();
        var (email, domainUserId) = await CrearAdministradorAsync();

        var codigo = await Ejecutar(
            Insumos(email, PasswordNueva, repetida: "OtraClave3$"),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId));

        codigo.Should().Be(ResetAdminPassword.ExitEntrada);

        using var alcance = _fixture.Provider.CreateScope();
        var um = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        var identity = await um.FindByEmailAsync(email);
        (await um.CheckPasswordAsync(identity!, PasswordOriginal)).Should().BeTrue();
        (await db.AuditLogs.CountAsync()).Should().Be(0, "sin confirmación no se escribe auditoría");
    }

    [Fact]
    public async Task SinFraseDeConfirmacion_Cancela_NoCambiaNada()
    {
        await _fixture.LimpiarAsync();
        var (email, domainUserId) = await CrearAdministradorAsync();

        var codigo = await Ejecutar(
            Insumos(email, PasswordNueva, confirmacion: "si"),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId));

        codigo.Should().Be(ResetAdminPassword.ExitEntrada);

        using var alcance = _fixture.Provider.CreateScope();
        var um = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        var identity = await um.FindByEmailAsync(email);
        (await um.CheckPasswordAsync(identity!, PasswordOriginal)).Should().BeTrue();
        (await db.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PasswordVacia_EsRechazada()
    {
        await _fixture.LimpiarAsync();
        var (email, domainUserId) = await CrearAdministradorAsync();

        var codigo = await Ejecutar(
            Insumos(email, string.Empty),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId));

        codigo.Should().Be(ResetAdminPassword.ExitEntrada);
    }

    // ── 3. Rechazos: cuenta, vínculo y rol ────────────────────────────────

    [Fact]
    public async Task CuentaInexistente_EsRechazada_SinCrearNada()
    {
        await _fixture.LimpiarAsync();
        var (_, domainUserId) = await CrearAdministradorAsync();
        var emailFantasma = EmailUnico("fantasma");

        var codigo = await Ejecutar(
            Insumos(emailFantasma, PasswordNueva),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId));

        codigo.Should().Be(ResetAdminPassword.ExitCuentaNoEncontrada,
            "el comando NO crea cuentas: para eso existe init-admin");

        using var alcance = _fixture.Provider.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        (await db.Set<ApplicationUser>().CountAsync()).Should().Be(1);
        (await db.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task VinculoDeDominioDistinto_EsRechazado_SinCambiarLaContrasena()
    {
        await _fixture.LimpiarAsync();
        var (email, domainUserId) = await CrearAdministradorAsync();

        // Se declara un perfil esperado DISTINTO del real.
        var codigo = await Ejecutar(
            Insumos(email, PasswordNueva),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId + 1000));

        codigo.Should().Be(ResetAdminPassword.ExitVinculo);

        using var alcance = _fixture.Provider.CreateScope();
        var um = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var identity = await um.FindByEmailAsync(email);
        (await um.CheckPasswordAsync(identity!, PasswordOriginal)).Should().BeTrue(
            "un vínculo inesperado aborta antes de tocar el hash");
    }

    [Fact]
    public async Task CuentaSinVinculoDeDominio_EsRechazada()
    {
        await _fixture.LimpiarAsync();
        var (email, domainUserId) = await CrearAdministradorAsync(vincular: false);

        var codigo = await Ejecutar(
            Insumos(email, PasswordNueva),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId));

        codigo.Should().Be(ResetAdminPassword.ExitVinculo);
    }

    [Fact]
    public async Task CuentaSinRolAdministrador_EsRechazada_YNoSeLeAsignaElRol()
    {
        await _fixture.LimpiarAsync();
        var (email, domainUserId) = await CrearAdministradorAsync(asignarRol: false);

        var codigo = await Ejecutar(
            Insumos(email, PasswordNueva),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId));

        codigo.Should().Be(ResetAdminPassword.ExitRol);

        using var alcance = _fixture.Provider.CreateScope();
        var um = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var identity = await um.FindByEmailAsync(email);

        (await um.IsInRoleAsync(identity!, RolAdmin)).Should().BeFalse(
            "el comando verifica roles; jamás los concede");
        (await um.CheckPasswordAsync(identity!, PasswordOriginal)).Should().BeTrue();
    }

    [Fact]
    public async Task PracticanteSinRolAdmin_NoEsPromovido()
    {
        await _fixture.LimpiarAsync();
        var (email, domainUserId) = await CrearAdministradorAsync(asignarRol: false);

        using (var alcance = _fixture.Provider.CreateScope())
        {
            var um = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var rm = alcance.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await rm.CreateAsync(new IdentityRole(RolPracticante));
            var identity = await um.FindByEmailAsync(email);
            await um.AddToRoleAsync(identity!, RolPracticante);
        }

        var codigo = await Ejecutar(
            Insumos(email, PasswordNueva),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId));

        codigo.Should().Be(ResetAdminPassword.ExitRol);

        using var verificacion = _fixture.Provider.CreateScope();
        var userManager = verificacion.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var cuenta = await userManager.FindByEmailAsync(email);
        (await userManager.IsInRoleAsync(cuenta!, RolPracticante)).Should().BeTrue("rol intacto");
        (await userManager.IsInRoleAsync(cuenta!, RolAdmin)).Should().BeFalse("no se promueve");
    }

    // ── 4. Rechazos: entorno, base y uso ──────────────────────────────────

    [Fact]
    public async Task FueraDeDevelopment_EsRechazado_SinPedirDatos()
    {
        await _fixture.LimpiarAsync();
        var (_, domainUserId) = await CrearAdministradorAsync();
        var (insumos, fuePedido) = InsumosConMarca();

        var codigo = await Ejecutar(
            insumos,
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId),
            entorno: _fixture.EntornoProduccion);

        codigo.Should().Be(ResetAdminPassword.ExitEntorno);
        fuePedido().Should().BeFalse("el entorno se valida antes de pedir credenciales");
    }

    [Fact]
    public async Task BaseNoAutorizada_EsRechazada_SinPedirDatos()
    {
        await _fixture.LimpiarAsync();
        var (_, domainUserId) = await CrearAdministradorAsync();
        var (insumos, fuePedido) = InsumosConMarca();

        // La conexión es la base AISLADA, pero se declara un nombre esperado
        // distinto: la guarda debe abortar antes de pedir contraseña.
        var opciones = new ResetAdminPassword.Opciones(
            DomainUserIdEsperado: domainUserId,
            BaseEsperada: "MiCaminoBJJ_Web_TFI_2026_Test_OtraBaseInexistente",
            CarpetaRespaldo: _fixture.CarpetaRespaldo,
            EjecutarRespaldo: true);

        var codigo = await Ejecutar(
            insumos,
            new[] { ResetAdminPassword.Comando },
            opciones);

        codigo.Should().Be(ResetAdminPassword.ExitBase);
        fuePedido().Should().BeFalse();
    }

    [Fact]
    public async Task ServidorNoAutorizado_EsRechazado()
    {
        await _fixture.LimpiarAsync();
        var (_, domainUserId) = await CrearAdministradorAsync();
        var (insumos, fuePedido) = InsumosConMarca();

        // Cadena declarada hacia OTRO servidor con el nombre de la base de
        // prueba: el guard rechaza el servidor antes de conectar.
        var cadenaAjena =
            $"Server=localhost;Database={_fixture.BasePrueba};Trusted_Connection=True;TrustServerCertificate=True";

        var codigo = await Ejecutar(
            insumos,
            new[] { ResetAdminPassword.Comando },
            new ResetAdminPassword.Opciones(domainUserId, _fixture.BasePrueba, _fixture.CarpetaRespaldo, false),
            cadenaConexion: cadenaAjena);

        codigo.Should().Be(ResetAdminPassword.ExitBase);
        fuePedido().Should().BeFalse();
    }

    [Fact]
    public async Task ArgumentosConPassword_SonRechazados_SinPedirDatos()
    {
        await _fixture.LimpiarAsync();
        var (insumos, fuePedido) = InsumosConMarca();

        var codigo = await Ejecutar(
            insumos,
            new[] { ResetAdminPassword.Comando, "--password=NoPasaPorAqui!" });

        codigo.Should().Be(ResetAdminPassword.ExitUso,
            "los secretos jamás se aceptan por argumentos");
        fuePedido().Should().BeFalse();

        var ayuda = await Ejecutar(insumos, new[] { ResetAdminPassword.Comando, "--help" });
        ayuda.Should().Be(ResetAdminPassword.ExitUso);
        fuePedido().Should().BeFalse();
    }

    [Fact]
    public async Task NoAceptaElComandoDeInitAdmin()
    {
        // Los comandos son INDEPENDIENTES: reset-admin-password no se dispara
        // con init-admin ni viceversa (evita confundir recuperación con alta).
        ResetAdminPassword.EsComando(new[] { "init-admin" }).Should().BeFalse();
        ResetAdminPassword.EsComando(new[] { ResetAdminPassword.Comando }).Should().BeTrue();
        AdminBootstrap.EsComando(new[] { ResetAdminPassword.Comando }).Should().BeFalse();
        AdminBootstrap.EsComando(new[] { "init-admin" }).Should().BeTrue();

        await Task.CompletedTask;
    }

    // ── 5. Aislamiento: otros usuarios no se tocan ────────────────────────

    [Fact]
    public async Task OtrosUsuarios_NoSeModifican()
    {
        await _fixture.LimpiarAsync();
        var (emailAdmin, domainUserId) = await CrearAdministradorAsync();

        // Segundo usuario (práctico) con su propia contraseña.
        var emailOtro = EmailUnico("practico");
        int domainUserIdOtro;
        using (var alcance = _fixture.Provider.CreateScope())
        {
            var sp = alcance.ServiceProvider;
            var db = sp.GetRequiredService<WebTfDbContext>();
            var um = sp.GetRequiredService<UserManager<ApplicationUser>>();
            var rm = sp.GetRequiredService<RoleManager<IdentityRole>>();

            var perfil = new User
            {
                Name = "Practicante de prueba",
                Email = emailOtro,
                PasswordHash = string.Empty,
                PasswordSalt = string.Empty,
                BeltRank = "Azul",
                Role = UserRole.Practicante,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(perfil);
            await db.SaveChangesAsync();
            domainUserIdOtro = perfil.Id;

            var identity = new ApplicationUser
            {
                UserName = emailOtro,
                Email = emailOtro,
                DomainUserId = perfil.Id
            };
            var creacion = await um.CreateAsync(identity, "Practico1!");
            creacion.Succeeded.Should().BeTrue();

            if (!await rm.RoleExistsAsync(RolPracticante))
                await rm.CreateAsync(new IdentityRole(RolPracticante));
            await um.AddToRoleAsync(identity, RolPracticante);
        }

        string stampOtroAntes, hashOtroAntes;
        using (var alcance = _fixture.Provider.CreateScope())
        {
            var um = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var otro = await um.FindByEmailAsync(emailOtro);
            stampOtroAntes = await um.GetSecurityStampAsync(otro!);
            hashOtroAntes = otro!.PasswordHash!;
        }

        var codigo = await Ejecutar(
            Insumos(emailAdmin, PasswordNueva),
            new[] { ResetAdminPassword.Comando },
            OpcionesPrueba(domainUserId));

        codigo.Should().Be(ResetAdminPassword.ExitOk);

        using var verificacion = _fixture.Provider.CreateScope();
        var userManager = verificacion.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbVerificacion = verificacion.ServiceProvider.GetRequiredService<WebTfDbContext>();

        var otroFinal = await userManager.FindByEmailAsync(emailOtro);
        (await userManager.CheckPasswordAsync(otroFinal!, "Practico1!")).Should().BeTrue(
            "la contraseña del otro usuario no se toca");
        (await userManager.GetSecurityStampAsync(otroFinal!)).Should().Be(stampOtroAntes,
            "el SecurityStamp ajeno no se rota");
        otroFinal!.PasswordHash.Should().Be(hashOtroAntes);
        otroFinal.DomainUserId.Should().Be(domainUserIdOtro);
        (await userManager.IsInRoleAsync(otroFinal, RolPracticante)).Should().BeTrue();

        var perfilOtro = await dbVerificacion.Users.AsNoTracking().SingleAsync(u => u.Id == domainUserIdOtro);
        perfilOtro.Role.Should().Be(UserRole.Practicante, "el rol de dominio ajeno no se modifica");
        perfilOtro.BeltRank.Should().Be("Azul");

        // Solo una auditoría: la del administrador operado.
        var auditorias = await dbVerificacion.AuditLogs.AsNoTracking().ToListAsync();
        auditorias.Should().HaveCount(1);
        auditorias[0].EntityId.Should().Be(domainUserId);
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
}
