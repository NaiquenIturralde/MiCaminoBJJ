using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Web.Persistence;

namespace MiCaminoBJJ.Web.Localization;

/// <summary>
/// Middleware que aplica la preferencia de idioma PERSISTIDA del practicante.
///
/// Orden dentro del pipeline:
/// 1. <c>UseRequestLocalization</c> (mecanismo estándar de ASP.NET Core)
///    resuelve la cultura por cookie → <c>Accept-Language</c> → español por
///    defecto.
/// 2. Este middleware, que se ejecuta DESPUÉS de la autenticación, consulta la
///    preferencia guardada en <c>UserSettings.Language</c> y la aplica si el
///    visitante es un practicante autenticado.
///
/// Así la preferencia persistida es la fuente de verdad (sobrevive a cerrar
/// sesión, recargar y volver a ingresar) sin dejar de usar el pipeline
/// oficial de localización.
///
/// Si la base no está disponible se registra el fallo y se conserva la cultura
/// resuelta por el pipeline estándar: la web nunca deja de funcionar por un
/// problema de idioma.
/// </summary>
public sealed class PreferenciaIdiomaMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PreferenciaIdiomaMiddleware> _logger;

    public PreferenciaIdiomaMiddleware(
        RequestDelegate next,
        ILogger<PreferenciaIdiomaMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Aplica la preferencia persistida (si existe) y continúa la cadena.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, WebTfDbContext db)
    {
        ArgumentNullException.ThrowIfNull(context);

        var codigo = await ResolverPreferenciaAsync(context, db);

        if (codigo is not null)
            AplicarCultura(context, codigo);

        await _next(context);
    }

    /// <summary>
    /// Devuelve el idioma persistido del practicante autenticado o null
    /// (visitante anónimo, sin preferencia, o base no disponible).
    /// </summary>
    private async Task<string?> ResolverPreferenciaAsync(HttpContext context, WebTfDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return null;

        // Los recursos estáticos (css, js, imágenes, fuentes) NO llevan
        // texto de interfaz: se evita consultar la base en cada archivo.
        if (RecursosEstaticos.EsEstatico(context.Request.Path.Value))
            return null;

        var email = context.User.Identity.Name;
        if (string.IsNullOrWhiteSpace(email))
            return null;

        try
        {
            // Una sola consulta: perfil de dominio + preferencia (1:1).
            var idioma = await db.Users
                .AsNoTracking()
                .Where(u => u.Email == email)
                .Select(u => u.Settings == null ? null : u.Settings.Language)
                .FirstOrDefaultAsync(context.RequestAborted);

            return Idiomas.Normalizar(idioma);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "No se pudo leer la preferencia de idioma del usuario; se conserva la cultura del pipeline estándar.");

            return null;
        }
    }

    /// <summary>
    /// Fija la cultura de la petición (y la de la vista) con el idioma
    /// elegido, actualizando también el <see cref="IRequestCultureFeature"/>
    /// para que sea coherente con el resto del pipeline.
    /// </summary>
    private static void AplicarCultura(HttpContext context, string codigo)
    {
        var cultura = CultureInfo.GetCultureInfo(codigo);

        CultureInfo.CurrentCulture = cultura;
        CultureInfo.CurrentUICulture = cultura;

        var feature = context.Features.Get<IRequestCultureFeature>();
        if (feature is not null)
        {
            // IRequestCultureFeature es de SOLO LECTURA: para cambiar la
            // cultura hay que reinsertar el feature con la nueva RequestCulture
            // (se conserva el proveedor que la resolvió).
            context.Features.Set<IRequestCultureFeature>(new RequestCultureFeature(
                new RequestCulture(cultura.Name, cultura.Name),
                feature.Provider));
        }
    }
}
