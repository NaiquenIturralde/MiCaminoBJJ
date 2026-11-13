namespace MiCaminoBJJ.Web.Localization;

/// <summary>
/// Reconoce las rutas que sirven un ARCHIVO ESTÁTICO (css, js, imágenes,
/// fuentes, etc.).
///
/// Uso: el middleware de preferencia de idioma NO consulta la base para estas
/// rutas, porque no muestran texto de interfaz. Sin esta salvedad, cada
/// archivo descargado (imágenes de cinturones, scripts, hojas de estilo)
/// disparaba una consulta extra a la base de datos.
/// </summary>
public static class RecursosEstaticos
{
    /// <summary>Extensiones servidas como archivo estático por la web.</summary>
    private static readonly HashSet<string> Extensiones = new(StringComparer.OrdinalIgnoreCase)
    {
        ".css", ".js", ".map", ".json", ".txt", ".xml",
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".avif", ".ico", ".bmp",
        ".woff", ".woff2", ".ttf", ".otf", ".eot",
        ".mp4", ".webm", ".mp3", ".pdf", ".zip"
    };

    /// <summary>
    /// Indica si la ruta apunta a un archivo estático (y por tanto no necesita
    /// la preferencia de idioma).
    /// </summary>
    public static bool EsEstatico(string? ruta)
    {
        if (string.IsNullOrEmpty(ruta))
            return false;

        // Se corta en '?' y '#' por si la ruta trae query string.
        var corte = ruta.IndexOfAny(['?', '#']);
        if (corte >= 0)
            ruta = ruta[..corte];

        var extension = Path.GetExtension(ruta);
        return extension.Length > 0 && Extensiones.Contains(extension);
    }
}
