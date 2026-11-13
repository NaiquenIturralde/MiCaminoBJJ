using Microsoft.AspNetCore.Localization;

namespace MiCaminoBJJ.Web.Localization;

/// <summary>
/// Proveedor de cultura que lee la preferencia de idioma de la COOKIE
/// <c>MiCaminoBJJ.Idioma</c>.
///
/// Es el mecanismo estándar de ASP.NET Core para transportar la preferencia
/// del visitante: se usa en el pipeline oficial de RequestLocalization y
/// permite que las visitas ANÓNIMAS (páginas públicas, login) ya respeten el
/// idioma elegido. Para los practicantes autenticados, la preferencia
/// PERSISTIDA en <c>UserSettings.Language</c> es la que manda (ver
/// <see cref="PreferenciaIdiomaMiddleware"/>).
///
/// Si la cookie no existe o contiene un idioma no soportado, el proveedor
/// devuelve null y el pipeline sigue con el siguiente proveedor.
/// </summary>
public sealed class IdiomaCookieRequestCultureProvider : RequestCultureProvider
{
    /// <inheritdoc />
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var cookie = httpContext.Request.Cookies[Idiomas.NombreCookie];
        var codigo = Idiomas.Normalizar(cookie);

        if (codigo is null)
            return Task.FromResult<ProviderCultureResult?>(null);

        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(codigo));
    }
}
