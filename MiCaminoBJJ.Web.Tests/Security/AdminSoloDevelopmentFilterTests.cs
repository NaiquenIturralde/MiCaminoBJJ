using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using MiCaminoBJJ.Web.Filters;

namespace MiCaminoBJJ.Web.Tests.Security;

/// <summary>
/// Etapa 4B — comportamiento del gate server-side de Development
/// (prueba funcional del filtro, además de las de metadatos existentes):
/// fuera de Development cualquier acción administrativa queda en HTTP 404
/// ANTES de cualquier otra verificación.
/// </summary>
public class AdminSoloDevelopmentFilterTests
{
    private static AuthorizationFilterContext CrearContexto(string environmentName)
    {
        var services = new ServiceCollection()
            .AddSingleton<IWebHostEnvironment>(new EntornoFalso(environmentName))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(
            httpContext, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());

        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task FueraDeDevelopment_RespondeNotFound_SinInvocarLaAccion(string environment)
    {
        var filtro = new AdminSoloDevelopmentAttribute();
        var contexto = CrearContexto(environment);

        await filtro.OnAuthorizationAsync(contexto);

        contexto.Result.Should().BeOfType<NotFoundResult>(
            $"en {environment} el módulo administrativo no existe (404), sin excepciones ni divulgación");
    }

    [Theory]
    [InlineData("Development")]
    public async Task EnDevelopment_DejaPasar_Autorizacion(string environment)
    {
        var filtro = new AdminSoloDevelopmentAttribute();
        var contexto = CrearContexto(environment);

        await filtro.OnAuthorizationAsync(contexto);

        contexto.Result.Should().BeNull("el gate no interviene en Development: sigue la autorización normal");
    }

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
