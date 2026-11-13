using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using MiCaminoBJJ.Web.Controllers;
using MiCaminoBJJ.Web.Filters;
using MiCaminoBJJ.Web.Services.Admin;
using MiCaminoBJJ.Web.ViewModels.Admin;

namespace MiCaminoBJJ.Web.Tests.Controllers;

/// <summary>
/// Etapa 3 — acceso administrativo local:
/// - Login: credencial validada en servidor, emisión/cierre de sesión y
///   protección contra open redirect.
/// - Metadatos de SEGURIDAD (reflexión): [Authorize] y [AdminSoloDevelopment]
///   en el CRUD, y [ValidateAntiForgeryToken] en TODOS los POST.
/// </summary>
public class AdminAccesoControllerTests
{
    private readonly FakeCredencial _credencial = new();
    private readonly FakeSesion _sesion = new();
    private readonly AdminAccesoController _sut;

    public AdminAccesoControllerTests()
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());

        _sut = new AdminAccesoController(_credencial, _sesion, NullLogger<AdminAccesoController>.Instance)
        {
            ControllerContext = new ControllerContext(actionContext)
        };
        _sut.Url = new UrlHelper(actionContext);
    }

    // ── Login ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_GET_Habilitado_VuelveVistaSinErrores()
    {
        var result = _sut.Login(returnUrl: "/AdminTecnicas");

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.Model.Should().BeOfType<AdminLoginViewModel>();
        _sut.ModelState.IsValid.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Login_GET_Deshabilitado_MuestraMensaje_YNoEmiteSesion()
    {
        _credencial.Habilitado = false;

        var result = _sut.Login(returnUrl: null);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.Model.Should().BeOfType<AdminLoginViewModel>();
        _sut.ModelState[string.Empty]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("deshabilitado");
        _sesion.SignInCalls.Should().Be(0);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Login_POST_PasswordIncorrecta_NoEmiteSesion_YMarcaError()
    {
        _credencial.PasswordCorrecta = false;

        var result = await _sut.Login(new AdminLoginViewModel { Password = "incorrecta" });

        result.Should().BeOfType<ViewResult>();
        _sesion.SignInCalls.Should().Be(0, "una credencial inválida no debe crear sesión");
        _sut.ModelState[string.Empty]!.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("Credencial incorrecta.");
    }

    [Fact]
    public async Task Login_POST_Correcta_EmiteSesion_YRedirigeAlCrud()
    {
        _credencial.PasswordCorrecta = true;

        var result = await _sut.Login(new AdminLoginViewModel { Password = "secreta" });

        var redirect = result.Should().BeOfType<LocalRedirectResult>().Subject;
        redirect.Url.Should().Be("/AdminTecnicas");
        _sesion.SignInCalls.Should().Be(1);
        _sesion.UltimoUsuario.Should().Be(_credencial.Usuario);
    }

    [Fact]
    public async Task Login_POST_ConservaReturnUrlLocal()
    {
        _credencial.PasswordCorrecta = true;

        var result = await _sut.Login(new AdminLoginViewModel { Password = "secreta", ReturnUrl = "/AdminTecnicas/Edit/3" });

        var redirect = result.Should().BeOfType<LocalRedirectResult>().Subject;
        redirect.Url.Should().Be("/AdminTecnicas/Edit/3");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Login_POST_RechazaReturnUrlExterno_NoOpenRedirect()
    {
        _credencial.PasswordCorrecta = true;

        var result = await _sut.Login(new AdminLoginViewModel
        {
            Password = "secreta",
            ReturnUrl = "https://ejemplo-malo.com/robar-sesion"
        });

        var redirect = result.Should().BeOfType<LocalRedirectResult>().Subject;
        redirect.Url.Should().Be("/AdminTecnicas", "una URL externa jamás debe ser destino de redirección");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Login_POST_Deshabilitado_NoValidaNiEmiteSesion()
    {
        _credencial.Habilitado = false;
        _credencial.PasswordCorrecta = true;

        var result = await _sut.Login(new AdminLoginViewModel { Password = "secreta" });

        result.Should().BeOfType<ViewResult>();
        _sesion.SignInCalls.Should().Be(0, "sin credencial configurada no se emite cookie");
        _sut.ModelState[string.Empty]!.Errors.Should().ContainSingle();
        _credencial.ValidacionesLlamadas.Should().Be(0, "ni siquiera se debe intentar validar");
    }

    [Fact]
    public async Task Logout_POST_CierraSesion_YVuelveALogin()
    {
        var result = await _sut.Logout();

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(AdminAccesoController.Login));
        _sesion.SignOutCalls.Should().Be(1);
    }

    // ── Metadatos de seguridad (reflexión) ────────────────────────────────

    [Fact]
    public void AdminTecnicas_Requiere_Autorizacion_Y_EntornoDevelopment()
    {
        var tipo = typeof(AdminTecnicasController);

        Assert.NotNull(tipo.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(tipo.GetCustomAttribute<AdminSoloDevelopmentAttribute>());
        Assert.Null(tipo.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.DoesNotContain(
            tipo.GetMethods(),
            m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
    }

    [Fact]
    public void AdminTecnicas_TodosLosPost_TienenProteccionAntiforgery()
    {
        var post = typeof(AdminTecnicasController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() is not null)
            .ToList();

        post.Should().HaveCount(4, "Create, Edit, Deactivate y Reactivate son POST");

        foreach (var accion in post)
        {
            Assert.True(
                accion.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null,
                $"{accion.Name} debe declarar [ValidateAntiForgeryToken]");
        }

        // Ninguna acción (GET incluida) puede eximirse con AllowAnonymous.
        Assert.DoesNotContain(
            typeof(AdminTecnicasController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null);
    }

    [Fact]
    public void Acceso_EstaReservadoADevelopment_YsusPost_TienenAntiforgery()
    {
        var tipo = typeof(AdminAccesoController);

        Assert.NotNull(tipo.GetCustomAttribute<AdminSoloDevelopmentAttribute>());

        var post = tipo
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() is not null)
            .ToList();

        post.Should().HaveCount(2, "Login (POST) y Logout (POST)");

        foreach (var accion in post)
        {
            Assert.True(
                accion.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null,
                $"{accion.Name} debe declarar [ValidateAntiForgeryToken]");
        }
    }

    // ── Fakes ─────────────────────────────────────────────────────────────

    private sealed class FakeCredencial : IDevAdminCredentialValidator
    {
        public bool Habilitado { get; set; } = true;
        public bool PasswordCorrecta { get; set; }
        public int ValidacionesLlamadas { get; private set; }

        public bool EstaHabilitado => Habilitado;
        public string Usuario => DevAdminCredential.DefaultUser;

        public bool PasswordValido(string? password)
        {
            ValidacionesLlamadas++;
            return PasswordCorrecta;
        }
    }

    private sealed class FakeSesion : IDevAdminSession
    {
        public int SignInCalls { get; private set; }
        public int SignOutCalls { get; private set; }
        public string? UltimoUsuario { get; private set; }

        public Task SignInAsync(HttpContext httpContext, string usuario)
        {
            SignInCalls++;
            UltimoUsuario = usuario;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext httpContext)
        {
            SignOutCalls++;
            return Task.CompletedTask;
        }
    }
}
