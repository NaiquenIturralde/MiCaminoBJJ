using System.Security.Claims;
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
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Web.Controllers;
using MiCaminoBJJ.Web.Identity;
using MiCaminoBJJ.Web.Localization;
using MiCaminoBJJ.Web.Persistence;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Services.Perfil;
using MiCaminoBJJ.Web.Tests.Infrastructure;
using MiCaminoBJJ.Web.ViewModels;

namespace MiCaminoBJJ.Web.Tests.Localization;

/// <summary>
/// Pruebas de la PREFERENCIA DE IDIOMA del practicante (persistencia real en
/// SQL Server sobre una base AISLADA y única por ejecución).
///
/// Cubre el ciclo completo exigido por el TFI:
/// - Guardar el idioma elegido (Español / English / Português).
/// - Releerlo después (persistencia real, no memoria).
/// - Cambiarlo de un idioma a otro sin duplicar la fila de preferencias.
/// - La pantalla de Configuración devuelve el idioma vigente y el catálogo de
///   los tres idiomas.
/// - Un idioma NO soportado se rechaza y NO se guarda.
/// - La cookie de idioma se emite para las visitas anónimas.
///
/// No se modifican usuarios ni roles: solo la fila de preferencias del propio
/// practicante de prueba.
/// </summary>
[Collection("SqlServerLocalDbCuenta")]
public sealed class PreferenciaIdiomaTests : IClassFixture<PreferenciaIdiomaTests.Fixture>
{
    public sealed class Fixture : IAsyncLifetime
    {
        private readonly List<IServiceScope> _alcances = new();

        /// <summary>Base aislada EXCLUSIVA de esta ejecución.</summary>
        public TestDatabaseLease Lease { get; } = TestDatabaseLease.Create("Idioma");

        public ServiceProvider Provider { get; private set; } = null!;
        public IHttpContextAccessor Accesor { get; private set; } = null!;
        public string Email { get; } = $"idioma-tfi-{Guid.NewGuid():N}@example.com";
        public string Password { get; } = "Abcdef1!";

        /// <summary>Id del perfil de dominio del practicante de prueba.</summary>
        public int PerfilId { get; private set; }

        public IServiceScope CrearAlcance()
        {
            var alcance = Provider.CreateScope();
            _alcances.Add(alcance);
            return alcance;
        }

        public async Task InitializeAsync()
        {
            var cadena = Lease.ConnectionString;
            TestDatabaseGuard.EnsureIsolated(cadena);
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

            using var alcance = Provider.CreateScope();
            var userManager = alcance.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
            TestDatabaseGuard.EnsureIsolated(db, Lease.DatabaseName);

            var identity = new ApplicationUser { UserName = Email, Email = Email };
            var creacion = await userManager.CreateAsync(identity, Password);
            if (!creacion.Succeeded)
                throw new InvalidOperationException(
                    "No se pudo crear el usuario de prueba: " + string.Join("; ", creacion.Errors.Select(e => e.Description)));

            var perfil = new User
            {
                Name = "Idioma TFI",
                Email = Email,
                PasswordHash = string.Empty,
                PasswordSalt = string.Empty,
                BeltRank = "Blanco",
                Role = UserRole.Practicante,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(perfil);
            await db.SaveChangesAsync();

            PerfilId = perfil.Id;
        }

        public async Task DisposeAsync()
        {
            foreach (var alcance in _alcances)
                alcance.Dispose();

            await Provider.DisposeAsync();
            await Lease.DropAsync();
        }
    }

    private readonly Fixture _fixture;

    public PreferenciaIdiomaTests(Fixture fixture) => _fixture = fixture;

    // ── Infraestructura ───────────────────────────────────────────────────

    private HttpContext HttpAutenticado()
    {
        var http = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var identidad = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, _fixture.Email)],
            MiCaminoBJJ.Web.Security.IdentitySetup.AuthSchemeName);

        http.User = new ClaimsPrincipal(identidad);
        return http;
    }

    private CuentaController CrearController(HttpContext http)
    {
        var alcance = _fixture.CrearAlcance();
        var sp = alcance.ServiceProvider;

        _fixture.Accesor.HttpContext = http;
        http.RequestServices = sp;

        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var controller = ActivatorUtilities.CreateInstance<CuentaController>(sp);
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(http, new VacioTempDataProvider());
        controller.Url = new UrlHelper(actionContext);
        return controller;
    }

    private static List<string> CookiesDe(HttpContext http)
    {
        if (!http.Response.Headers.TryGetValue("Set-Cookie", out var valores) || valores.Count == 0)
            return new List<string>();

        return valores.Select(v => v ?? string.Empty).ToList();
    }

    /// <summary>TempData vacío: el controlador no debe depender de él.</summary>
    private sealed class VacioTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }

    // ── Persistencia ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [InlineData("pt")]
    public async Task Guardar_YPersistirElIdiomaElegido(string idioma)
    {
        using var alcance = _fixture.CrearAlcance();
        var servicio = alcance.ServiceProvider.GetRequiredService<IPreferenciaIdiomaService>();

        await servicio.GuardarAsync(_fixture.PerfilId, idioma);

        var guardado = await servicio.ObtenerAsync(_fixture.PerfilId);
        guardado.Should().Be(idioma);
    }

    [Fact]
    public async Task Guardar_TresVeces_MantieneUnaSolaFilaDePreferencias()
    {
        using var alcance = _fixture.CrearAlcance();
        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        var servicio = alcance.ServiceProvider.GetRequiredService<IPreferenciaIdiomaService>();

        await servicio.GuardarAsync(_fixture.PerfilId, "en");
        await servicio.GuardarAsync(_fixture.PerfilId, "pt");
        await servicio.GuardarAsync(_fixture.PerfilId, "es");

        var filas = await db.UserSettings
            .AsNoTracking()
            .Where(a => a.UserId == _fixture.PerfilId)
            .ToListAsync();

        filas.Should().HaveCount(1, "la preferencia de idioma es 1:1 con el usuario");
        filas[0].Language.Should().Be("es");
    }

    [Fact]
    public async Task Guardar_ConIdiomaNoSoportado_EsRechazadoYNoGuardaNada()
    {
        using var alcance = _fixture.CrearAlcance();
        var servicio = alcance.ServiceProvider.GetRequiredService<IPreferenciaIdiomaService>();

        var accion = async () => await servicio.GuardarAsync(_fixture.PerfilId, "fr");

        await accion.Should().ThrowAsync<ArgumentException>();

        (await servicio.ObtenerAsync(_fixture.PerfilId)).Should().BeNull();
    }

    [Fact]
    public async Task Obtener_SinPreferencia_ReturnsNull()
    {
        using var alcance = _fixture.CrearAlcance();
        var servicio = alcance.ServiceProvider.GetRequiredService<IPreferenciaIdiomaService>();

        // Perfil sin fila de preferencias todavía.
        var perfilNuevo = new User
        {
            Name = "Sin preferencias",
            Email = $"sin-pref-{Guid.NewGuid():N}@example.com",
            PasswordHash = string.Empty,
            PasswordSalt = string.Empty,
            BeltRank = "Blanco",
            Role = UserRole.Practicante,
            CreatedAt = DateTime.UtcNow
        };

        var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
        db.Users.Add(perfilNuevo);
        await db.SaveChangesAsync();

        (await servicio.ObtenerAsync(perfilNuevo.Id)).Should().BeNull();
    }

    // ── Pantalla de Configuración ──────────────────────────────────────────

    [Fact]
    public async Task Configuracion_Get_MuestraElIdiomaVigenteYLosTresIdiomas()
    {
        using var alcance = _fixture.CrearAlcance();
        var servicio = alcance.ServiceProvider.GetRequiredService<IPreferenciaIdiomaService>();
        await servicio.GuardarAsync(_fixture.PerfilId, "pt");

        var http = HttpAutenticado();
        var controller = CrearController(http);

        var resultado = await controller.Configuracion();

        var vista = resultado.Should().BeOfType<ViewResult>().Subject;
        var model = vista.Model.Should().BeOfType<ConfiguracionViewModel>().Subject;

        model.Idioma.Should().Be("pt", "la pantalla muestra el idioma guardado");
        model.IdiomaActual.Should().Be("pt");
        model.Idiomas.Should().HaveCount(3);
        model.Idiomas.Select(o => o.Codigo).Should().BeEquivalentTo(["es", "en", "pt"]);
    }

    [Fact]
    public async Task Configuracion_Get_SinPreferencia_PorDefectoEnEspanol()
    {
        // El fixture es compartido por la clase: se borra cualquier
        // preferencia previa para que la prueba sea independiente del orden.
        using (var alcance = _fixture.CrearAlcance())
        {
            var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
            var previas = await db.UserSettings.Where(a => a.UserId == _fixture.PerfilId).ToListAsync();
            db.UserSettings.RemoveRange(previas);
            await db.SaveChangesAsync();
        }

        var http = HttpAutenticado();
        var controller = CrearController(http);

        var resultado = await controller.Configuracion();

        var model = resultado.Should().BeOfType<ViewResult>().Subject
            .Model.Should().BeOfType<ConfiguracionViewModel>().Subject;

        model.Idioma.Should().Be("es");
        model.Idiomas.Should().HaveCount(3);
    }

    [Fact]
    public async Task Configuracion_Post_GuardaElIdiomaYEmiteLaCookie()
    {
        var http = HttpAutenticado();
        var controller = CrearController(http);

        var resultado = await controller.Configuracion(new ConfiguracionViewModel { Idioma = "en" });

        // Patrón PRG: redirige al GET (no se vuelve a renderizar el formulario).
        var redirect = resultado.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Configuracion");

        // La preferencia quedó persistida.
        using var alcance = _fixture.CrearAlcance();
        var servicio = alcance.ServiceProvider.GetRequiredService<IPreferenciaIdiomaService>();
        (await servicio.ObtenerAsync(_fixture.PerfilId)).Should().Be("en");

        // La cookie transporta el idioma para las visitas anónimas.
        CookiesDe(http).Should().Contain(c => c.Contains(Idiomas.NombreCookie) && c.Contains("en"));
    }

    [Fact]
    public async Task Configuracion_Post_ConIdiomaInvalido_NoGuardaYDevuelveElFormulario()
    {
        var http = HttpAutenticado();
        var controller = CrearController(http);

        var resultado = await controller.Configuracion(new ConfiguracionViewModel { Idioma = "fr" });

        var vista = resultado.Should().BeOfType<ViewResult>().Subject;
        vista.Model.Should().BeOfType<ConfiguracionViewModel>();

        controller.ModelState.IsValid.Should().BeFalse("un idioma no soportado es inválido");

        using var alcance = _fixture.CrearAlcance();
        var servicio = alcance.ServiceProvider.GetRequiredService<IPreferenciaIdiomaService>();
        (await servicio.ObtenerAsync(_fixture.PerfilId)).Should().BeNull("no se guarda un idioma inválido");
    }

    [Fact]
    public async Task Configuracion_Post_CambiaDeIdioma_SinDuplicarLaPreferencia()
    {
        var http = HttpAutenticado();
        var controller = CrearController(http);

        await controller.Configuracion(new ConfiguracionViewModel { Idioma = "en" });

        using (var alcance = _fixture.CrearAlcance())
        {
            var servicio = alcance.ServiceProvider.GetRequiredService<IPreferenciaIdiomaService>();
            (await servicio.ObtenerAsync(_fixture.PerfilId)).Should().Be("en");
        }

        // Nueva "sesión": la preferencia sigue vigente y se puede cambiar.
        var http2 = HttpAutenticado();
        var controller2 = CrearController(http2);
        await controller2.Configuracion(new ConfiguracionViewModel { Idioma = "pt" });

        using (var alcance = _fixture.CrearAlcance())
        {
            var db = alcance.ServiceProvider.GetRequiredService<WebTfDbContext>();
            var servicio = alcance.ServiceProvider.GetRequiredService<IPreferenciaIdiomaService>();

            (await servicio.ObtenerAsync(_fixture.PerfilId)).Should().Be("pt");

            var filas = await db.UserSettings.AsNoTracking()
                .Where(a => a.UserId == _fixture.PerfilId)
                .ToListAsync();

            filas.Should().HaveCount(1);
        }
    }
}
