using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace MiCaminoBJJ.Web.Localization;

/// <summary>
/// Registro de los servicios de localización de la web (es / en / pt).
///
/// Reutiliza el mecanismo ESTÁNDAR de ASP.NET Core:
/// - <c>IStringLocalizer</c> para resolver textos desde los archivos .resx.
/// - <c>RequestLocalizationOptions</c> + <c>UseRequestLocalization</c> para
///   negociar la cultura de cada petición.
/// - <c>AddDataAnnotationsLocalization</c> para traducir los mensajes de
///   validación de los formularios.
///
/// No registra infraestructura de MAUI ni toca SQLite: es exclusivo de la web.
/// </summary>
public static class LocalizationSetup
{
    /// <summary>
    /// Registra el localizador de recursos y los servicios de preferencia de
    /// idioma.
    /// </summary>
    public static IServiceCollection AddWebLocalization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // ResourcesPath define la carpeta base de los .resx ("Resources").
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        // Preferencia persistida (UserSettings.Language) — scoped: usa el
        // WebTfDbContext de la petición.
        services.AddScoped<IPreferenciaIdiomaService, PreferenciaIdiomaService>();

        return services;
    }

    /// <summary>
    /// Configura la negociación de cultura: español por defecto, con la
    /// cookie de preferencia y la cabecera <c>Accept-Language</c> como
    /// proveedores, y los tres idiomas soportados.
    /// </summary>
    public static RequestLocalizationOptions CrearOpcionesRequestLocalization()
    {
        var opciones = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(Idiomas.Es),
            SupportedCultures = Idiomas.Culturas.ToList(),
            SupportedUICultures = Idiomas.Culturas.ToList()
        };

        // Se reemplaza la lista por defecto (query string → cookie → Accept-Language):
        // la query string NO debe cambiar el idioma de una aplicación con
        // preferencia persistida por usuario.
        opciones.RequestCultureProviders.Clear();
        opciones.RequestCultureProviders.Add(new IdiomaCookieRequestCultureProvider());
        opciones.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());

        return opciones;
    }

    /// <summary>
    /// Agrega el middleware que aplica la preferencia persistida del
    /// practicante. Debe registrarse DESPUÉS de la autenticación (necesita
    /// <c>HttpContext.User</c>) y DESPUÉS de <c>UseRequestLocalization</c>
    /// (para sobreescribir su resultado cuando exista una preferencia).
    /// </summary>
    public static IApplicationBuilder UsePreferenciaIdioma(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<PreferenciaIdiomaMiddleware>();
    }

    /// <summary>
    /// Aplica la cultura de un idioma a la petición actual.
    ///
    /// Se usa tras GUARDAR una preferencia: la cultura de la petición era la
    /// anterior (la que estaba persistida), así que sin esto el mensaje de
    /// confirmación se escribiría en el idioma viejo mientras la pantalla
    /// siguiente ya se muestra en el nuevo.
    /// </summary>
    public static void AplicarCultura(string codigo)
    {
        var cultura = CultureInfo.GetCultureInfo(Idiomas.Normalizar(codigo) ?? Idiomas.Es);

        CultureInfo.CurrentCulture = cultura;
        CultureInfo.CurrentUICulture = cultura;
    }
}
