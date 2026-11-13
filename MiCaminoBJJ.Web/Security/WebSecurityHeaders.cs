using Microsoft.AspNetCore.Http;

namespace MiCaminoBJJ.Web.Security;

/// <summary>
/// Cabeceras de seguridad HTTP de la aplicación (Etapa 4).
///
/// Corrección de hallazgo: sin <c>X-Frame-Options</c>, cualquier sitio podía
/// enmarcar el panel administrativo en un iframe (clickjacking / UI redress
/// sobre las acciones de desactivación y los formularios).
///
/// - <c>X-Frame-Options: DENY</c> → el sitio no se muestra dentro de iframes.
/// - <c>X-Content-Type-Options: nosniff</c> → impide que el navegador
///   reinterprete tipos MIME (p. ej. subir/servear algo como "imagen" que
///   resulte ser HTML/JS).
/// - <c>Referrer-Policy: strict-origin-when-cross-origin</c> → el origen
///   (solo esquema+host+puerto) se envía a dominios externos, pero la ruta,
///   la consulta y el fragmento NUNCA salen de la aplicación. En peticiones
///   del mismo origen se envía la URL completa, como antes.
///
///   POR QUÉ NO <c>same-origin</c>: con <c>same-origin</c> el navegador OMITE
///   la cabecera <c>Referer</c> en toda petición cruzada. El reproductor de
///   YouTube exige ese encabezado y, sin él, responde con el
///   <c>Error 153</c> ("Video player configuration error"), dejando la
///   técnica sin video. La política actual mantiene el aislamiento (no se
///   filtran rutas ni parámetros) y a la vez permite el embebido de video.
///
/// Función pura sobre <see cref="IHeaderDictionary"/> para poder probarse sin
/// levantar el pipeline. No se agrega una CSP completa: rompería los estilos
/// inline del módulo admin sin beneficio demostrable para la demo.
/// </summary>
public static class WebSecurityHeaders
{
    public const string FrameOptionsName = "X-Frame-Options";
    public const string FrameOptionsValue = "DENY";

    public const string ContentTypeOptionsName = "X-Content-Type-Options";
    public const string ContentTypeOptionsValue = "nosniff";

    public const string ReferrerPolicyName = "Referrer-Policy";

    /// <summary>
    /// Política de referencia: en mismo origen envía la URL completa; en otros
    /// dominios envía únicamente el origen (nunca la ruta). Necesaria para que
    /// el reproductor de YouTube acepte el embed (Error 153 sin <c>Referer</c>).
    /// </summary>
    public const string ReferrerPolicyValue = "strict-origin-when-cross-origin";

    public static void Apply(IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        headers[FrameOptionsName] = FrameOptionsValue;
        headers[ContentTypeOptionsName] = ContentTypeOptionsValue;
        headers[ReferrerPolicyName] = ReferrerPolicyValue;
    }
}
