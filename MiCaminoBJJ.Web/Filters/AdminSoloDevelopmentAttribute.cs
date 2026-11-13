using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MiCaminoBJJ.Web.Filters;

/// <summary>
/// El módulo administrativo SOLO existe en Development (Etapa 3).
///
/// Implementa IAsyncAuthorizationFilter (etapa de AUTORIZACIÓN, ANTES que el
/// antiforgery global y [Authorize]) con Order -2000 para que, fuera de
/// Development, CUALQUIER petición al módulo responda HTTP 404 uniforme
/// (también los POST sin token, que de otro modo verían primero el 400 del
/// antiforgery). El módulo no está "deshabilitado a medias": simplemente no
/// existe fuera de Development.
///
/// La verificación es SERVER-SIDE (IWebHostEnvironment del hosting): no
/// depende de ningún valor del navegador. Fail closed: sin entorno de hosting
/// no se habilita nada. Se combina con [Authorize] (cookie de Development).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class AdminSoloDevelopmentAttribute : Attribute, IAsyncAuthorizationFilter, IOrderedFilter
{
    /// <summary>Antes que los filtros globales/por defecto (antiforgery, [Authorize]).</summary>
    public int Order => -2000;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var environment = context.HttpContext.RequestServices.GetService<IWebHostEnvironment>();

        // Fail closed: sin entorno de hosting no se habilita nada.
        if (environment is null || !environment.IsDevelopment())
        {
            context.Result = new NotFoundResult();
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
