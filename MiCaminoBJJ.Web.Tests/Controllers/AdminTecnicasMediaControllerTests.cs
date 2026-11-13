using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Web.Controllers;
using MiCaminoBJJ.Web.Filters;
using MiCaminoBJJ.Web.Security;
using MiCaminoBJJ.Web.Services.Admin;
using MiCaminoBJJ.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;

namespace MiCaminoBJJ.Web.Tests.Controllers;

/// <summary>
/// Etapa multimedia — comportamiento del AdminTecnicasMediaController con un
/// servicio FALSO (sin base de datos): mensajes TempData, 404 por Id
/// inexistente, conservación de filtros, resguardo del intento de alta y
/// metadatos de seguridad (autorización Administrador + antiforgery en TODOS
/// los POST). La persistencia real se prueba en
/// Persistence/AdminTecnicasMediaIntegrationTests.
/// </summary>
public class AdminTecnicasMediaControllerTests
{
    private readonly FakeMediaCommandService _comandos = new();
    private readonly AdminTecnicasMediaController _sut;

    public AdminTecnicasMediaControllerTests()
    {
        _sut = new AdminTecnicasMediaController(_comandos, NullLogger<AdminTecnicasMediaController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        _sut.TempData = new TempDataDictionary(new DefaultHttpContext(), new VacioTempDataProvider());
    }

    private static AdminMediaAltaViewModel AltaValida() => new()
    {
        Url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
        Titulo = "Demo",
        Descripcion = "Nota"
    };

    private static AdminMediaEdicionViewModel EdicionValida() => new()
    {
        Url = "https://youtu.be/eDUjKxV6wb0",
        Titulo = "Editado",
        Descripcion = "Otra nota"
    };

    // ── Alta ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Agregar_POST_Valido_EjecutaComando_RedirigeAEdit_YMensajeDeExito()
    {
        _comandos.Resultado = ResultadoOperacion.Ok(7);

        var result = await _sut.Agregar(7, AltaValida(), buscar: "kimura", estado: false, cinturon: 3);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminTecnicasController.Edit));
        redirect.ControllerName.Should().Be("AdminTecnicas");
        redirect.RouteValues!["id"].Should().Be(7);
        redirect.RouteValues["buscar"].Should().Be("kimura", "los filtros del listado se conservan");
        redirect.RouteValues["estado"].Should().Be(false);
        redirect.RouteValues["cinturon"].Should().Be(3);
        _comandos.Llamadas.Should().Be(1);
        _comandos.UltimoId.Should().Be(7);
        _comandos.UltimoComando!.Url.Should().Contain("youtube.com/watch");
        _sut.TempData["Exito"].Should().NotBeNull();
    }

    [Fact]
    public async Task Agregar_POST_ConModeloInvalido_NoEjecutaComando_YResguardaElIntento()
    {
        var model = new AdminMediaAltaViewModel(); // Url obligatoria → inválido
        ValidarEnModelState(model, _sut);

        var result = await _sut.Agregar(7, model);

        result.Should().BeOfType<RedirectToActionResult>();
        _comandos.Llamadas.Should().Be(0, "un modelo inválido no debe llegar al servicio");
        _sut.TempData["Error"].Should().NotBeNull();
        _sut.TempData["Exito"].Should().BeNull();
        _sut.TempData["MediaUrlIntentada"].Should().BeOfType<string>();
        // El intento queda repoblado en Edit GET (probado en AdminTecnicasControllerTests).
    }

    [Fact]
    public async Task Agregar_POST_ConRechazoDeNegocio_MuestraError_SinExito()
    {
        _comandos.Resultado = ResultadoOperacion.Fallo("Ese video ya está asociado a esta técnica.");

        var result = await _sut.Agregar(7, AltaValida());

        result.Should().BeOfType<RedirectToActionResult>();
        _sut.TempData["Error"].Should().Be("Ese video ya está asociado a esta técnica.");
        _sut.TempData["Exito"].Should().BeNull();
    }

    [Fact]
    public async Task Agregar_POST_ConTecnicaInexistente_Responde404()
    {
        _comandos.Resultado = ResultadoOperacion.NoEncontrada();

        var result = await _sut.Agregar(999999, AltaValida());

        result.Should().BeOfType<NotFoundResult>();
        _sut.TempData["Exito"].Should().BeNull();
    }

    [Fact]
    public async Task Agregar_POST_SiElServicioFalla_MuestraErrorDeInfraestructura()
    {
        _comandos.Excepcion = new InvalidOperationException("SQL caído");

        var result = await _sut.Agregar(7, AltaValida());

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminTecnicasController.Edit));
        _sut.TempData["Exito"].Should().BeNull();
        _sut.TempData["Error"].Should().NotBeNull();
        _sut.TempData["Error"]?.ToString().Should().Contain("No se pudo completar la operación");
    }

    // ── Edición ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Editar_POST_ElIdDeLaRutaEsLaFuenteDeVerdad()
    {
        _comandos.Resultado = ResultadoOperacion.Ok(7);
        var model = EdicionValida();
        model.MediaId = 777777; // overposting: el form no debe decidir qué recurso se edita

        var result = await _sut.Editar(7, mediaId: 42, model);

        result.Should().BeOfType<RedirectToActionResult>();
        _comandos.UltimoMediaId.Should().Be(42, "el mediaId de la ruta manda sobre el del formulario");
        model.MediaId.Should().Be(42, "se re-normaliza desde la ruta antes de usarlo");
        _sut.TempData["Exito"].Should().NotBeNull();
    }

    [Fact]
    public async Task Editar_POST_ConRechazo_MuestraError_SinExito()
    {
        _comandos.Resultado = ResultadoOperacion.Fallo("La URL no corresponde a un proveedor de video soportado.");

        var result = await _sut.Editar(7, 42, EdicionValida());

        result.Should().BeOfType<RedirectToActionResult>();
        _sut.TempData["Error"]?.ToString().Should().Contain("proveedor de video");
        _sut.TempData["Exito"].Should().BeNull();
    }

    [Fact]
    public async Task Editar_POST_ConRecursoInexistente_Responde404()
    {
        _comandos.Resultado = ResultadoOperacion.NoEncontrada();

        var result = await _sut.Editar(7, 42, EdicionValida());

        result.Should().BeOfType<NotFoundResult>();
    }

    // ── Portada y desvinculación ───────────────────────────────────────────

    [Fact]
    public async Task MarcarPortada_POST_Ejecuta_YRedirigeConExito()
    {
        _comandos.Resultado = ResultadoOperacion.Ok(7);

        var result = await _sut.MarcarPortada(7, 42, buscar: "kimura");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.RouteValues!["id"].Should().Be(7);
        redirect.RouteValues["buscar"].Should().Be("kimura");
        _comandos.UltimoMarcarPortada.Should().BeTrue();
        _comandos.UltimoMediaId.Should().Be(42);
        _sut.TempData["Exito"].Should().NotBeNull();
    }

    [Fact]
    public async Task Quitar_POST_EjecutaDesvinculacion_YMensajeDejaClaroQueElRecursoSeConserva()
    {
        _comandos.Resultado = ResultadoOperacion.Ok(7);

        var result = await _sut.Quitar(7, 42);

        result.Should().BeOfType<RedirectToActionResult>();
        _comandos.UltimoDesvincular.Should().BeTrue();
        _comandos.UltimoMediaId.Should().Be(42);
        _sut.TempData["Exito"]?.ToString().Should().Contain("se conservó",
            "el mensaje debe aclarar que el archivo compartido NO se elimina");
    }

    [Fact]
    public async Task Quitar_POST_ConRecursoInexistente_Responde404()
    {
        _comandos.Resultado = ResultadoOperacion.NoEncontrada();

        var result = await _sut.Quitar(7, 42);

        result.Should().BeOfType<NotFoundResult>();
        _comandos.UltimoDesvincular.Should().BeTrue();
    }

    // ── Metadatos de seguridad (reflexión) ─────────────────────────────────

    [Fact]
    public void ControladorMedia_Requiere_AutorizacionAdministrador_YDevelopment()
    {
        var tipo = typeof(AdminTecnicasMediaController);

        tipo.GetCustomAttribute<AdminSoloDevelopmentAttribute>().Should().NotBeNull(
            "fuera de Development el módulo responde 404");

        var authorize = tipo.GetCustomAttribute<AuthorizeAttribute>();
        authorize.Should().NotBeNull();
        authorize!.Policy.Should().Be(AdminPolicies.AdministradorContenido,
            "la política explícita exige SIEMPRE el rol Administrador");

        tipo.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull(
            "nunca AllowAnonymous en acciones administrativas");

        tipo.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Should().NotContain(m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null);
    }

    [Fact]
    public void ControladorMedia_TodosLosPost_TienenProteccionAntiforgery()
    {
        var post = typeof(AdminTecnicasMediaController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() is not null)
            .ToList();

        post.Select(m => m.Name).Should().BeEquivalentTo(
            new[] { "Agregar", "Editar", "MarcarPortada", "Quitar" },
            "las cuatro acciones multimedia son POST");

        foreach (var accion in post)
        {
            accion.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>().Should().NotBeNull(
                $"{accion.Name} debe declarar [ValidateAntiForgeryToken]");
        }
    }

    [Fact]
    public void SeccionFormularios_NoExpone_PropiedadesDeEstadoEnAlta()
    {
        // Overposting: el formulario de alta solo puede enviar URL/título/descripción.
        var expuestas = typeof(AdminMediaAltaViewModel)
            .GetProperties()
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .ToList();

        expuestas.Should().Equal(new[] { "Url", "Titulo", "Descripcion" },
            "el alta no debe poder vincular Ids ni estados del dominio");
    }

    // ── Helpers y fakes ────────────────────────────────────────────────────

    private static void ValidarEnModelState(AdminMediaAltaViewModel model, Controller sut)
    {
        var resultados = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var contexto = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        if (System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                model, contexto, resultados, validateAllProperties: true))
            return;

        foreach (var error in resultados)
        {
            foreach (var member in error.MemberNames)
                sut.ModelState.AddModelError(member, error.ErrorMessage!);
            if (!error.MemberNames.Any())
                sut.ModelState.AddModelError(string.Empty, error.ErrorMessage!);
        }
    }

    private sealed class FakeMediaCommandService : IAdminTecnicasMediaCommandService
    {
        public ResultadoOperacion Resultado { get; set; } = ResultadoOperacion.Ok();
        public Exception? Excepcion { get; set; }
        public int Llamadas { get; private set; }
        public int? UltimoId { get; private set; }
        public int? UltimoMediaId { get; private set; }
        public MediaComando? UltimoComando { get; private set; }
        public bool UltimoMarcarPortada { get; private set; }
        public bool UltimoDesvincular { get; private set; }

        private ResultadoOperacion Registrar(int id, int? mediaId, MediaComando? comando)
        {
            Llamadas++;
            UltimoId = id;
            UltimoMediaId = mediaId;
            UltimoComando = comando;
            if (Excepcion is not null)
                throw Excepcion;
            return Resultado;
        }

        public Task<ResultadoOperacion> AgregarAsync(int tecnicaId, MediaComando comando, string actor) =>
            Task.FromResult(Registrar(tecnicaId, null, comando));

        public Task<ResultadoOperacion> EditarAsync(int tecnicaId, int mediaId, MediaComando comando, string actor) =>
            Task.FromResult(Registrar(tecnicaId, mediaId, comando));

        public Task<ResultadoOperacion> MarcarPortadaAsync(int tecnicaId, int mediaId, string actor)
        {
            UltimoMarcarPortada = true;
            return Task.FromResult(Registrar(tecnicaId, mediaId, null));
        }

        public Task<ResultadoOperacion> DesvincularAsync(int tecnicaId, int mediaId, string actor)
        {
            UltimoDesvincular = true;
            return Task.FromResult(Registrar(tecnicaId, mediaId, null));
        }
    }

    private sealed class VacioTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }
}
