using Microsoft.AspNetCore.Http;

namespace MiCaminoBJJ.Web.Services.Perfil;

/// <summary>
/// Validación de la fotografía de perfil en el SERVIDOR.
////
/// Tres capas de control (ninguna confía en lo que declara el navegador):
/// 1. EXTENSIÓN: solo .jpg/.jpeg/.png/.webp.
/// 2. TAMAÑO: máximo 2 MB.
/// 3. CONTENIDO REAL: se leen los primeros bytes del archivo y se comprueba
///    la firma binaria (JPEG / PNG / WEBP). Un archivo que NO es una imagen
///    (por ejemplo un .html o un .exe renombrado a .png) se rechaza aunque su
///    extensión y su Content-Type sean correctos.
///
/// El tipo MIME que se usa para servir el archivo es el DETECTADO, nunca el
/// declarado por el cliente.
/// </summary>
public static class PerfilFotoValidador
{
    /// <summary>Tamaño máximo admitido: 2 MB.</summary>
    public const long TamanoMaximoBytes = 2L * 1024 * 1024;

    /// <summary>Cantidad de bytes leídos para identificar el tipo real.</summary>
    private const int TamanoCabecera = 16;

    private static readonly string[] ExtensionesPermitidas = [".jpg", ".jpeg", ".png", ".webp"];

    /// <summary>Resultado de la validación.</summary>
    /// <param name="EsValido">true si el archivo puede almacenarse.</param>
    /// <param name="Error">Mensaje para la persona (nunca detalles técnicos).</param>
    /// <param name="MimeType">Tipo real detectado (null si no es válido).</param>
    /// <param name="Extension">Extensión canónica del tipo detectado.</param>
    public sealed record Resultado(bool EsValido, string? Error, string? MimeType, string? Extension)
    {
        public static Resultado Correcto(string mimeType, string extension) =>
            new(true, null, mimeType, extension);

        public static Resultado Incorrecto(string mensaje) =>
            new(false, mensaje, null, null);
    }

    /// <summary>Valida el archivo recibido (tipo, tamaño y contenido real).</summary>
    public static Resultado Validar(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return Resultado.Incorrecto("Seleccioná una imagen para tu foto de perfil.");

        if (archivo.Length > TamanoMaximoBytes)
            return Resultado.Incorrecto(
                "La foto no puede superar los 2 MB. Elegí una imagen más liviana.");

        var extension = Path.GetExtension(archivo.FileName ?? string.Empty).ToLowerInvariant();
        if (!ExtensionesPermitidas.Contains(extension))
            return Resultado.Incorrecto("Solo se admiten imágenes JPG, PNG o WEBP.");

        byte[] cabecera;
        using (var stream = archivo.OpenReadStream())
        {
            cabecera = new byte[TamanoCabecera];
            var leidos = stream.Read(cabecera, 0, cabecera.Length);
            if (leidos < cabecera.Length)
                Array.Resize(ref cabecera, leidos);
        }

        var detectado = DetectarTipo(cabecera);
        if (detectado is null)
            return Resultado.Incorrecto("El archivo no es una imagen válida.");

        // La extensión declarada debe corresponderse con el contenido real.
        if (!CoincideExtension(extension, detectado.Value.MimeType))
            return Resultado.Incorrecto("El contenido del archivo no coincide con su extensión.");

        // El Content-Type del navegador es INFORMATIVO: si viene informado y
        // contradice al contenido real, se rechaza (no se confía en él).
        var declarado = NormalizarTipoDeclarado(archivo.ContentType);
        if (declarado is not null && !string.Equals(declarado, detectado.Value.MimeType, StringComparison.Ordinal))
            return Resultado.Incorrecto("El tipo de archivo declarado no coincide con el contenido.");

        return Resultado.Correcto(detectado.Value.MimeType, detectado.Value.Extension);
    }

    /// <summary>
    /// Identifica el tipo real por la firma binaria del archivo.
    /// Devuelve null cuando el contenido no es una imagen admitida.
    /// </summary>
    private static (string MimeType, string Extension)? DetectarTipo(byte[] cabecera)
    {
        // JPEG: FF D8 FF
        if (cabecera.Length >= 3 && cabecera[0] == 0xFF && cabecera[1] == 0xD8 && cabecera[2] == 0xFF)
            return ("image/jpeg", ".jpg");

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (cabecera.Length >= 8
            && cabecera[0] == 0x89 && cabecera[1] == 0x50 && cabecera[2] == 0x4E && cabecera[3] == 0x47
            && cabecera[4] == 0x0D && cabecera[5] == 0x0A && cabecera[6] == 0x1A && cabecera[7] == 0x0A)
            return ("image/png", ".png");

        // WEBP: "RIFF" .... "WEBP"
        if (cabecera.Length >= 12
            && cabecera[0] == 'R' && cabecera[1] == 'I' && cabecera[2] == 'F' && cabecera[3] == 'F'
            && cabecera[8] == 'W' && cabecera[9] == 'E' && cabecera[10] == 'B' && cabecera[11] == 'P')
            return ("image/webp", ".webp");

        return null;
    }

    /// <summary>La extensión declarada es coherente con el tipo detectado.</summary>
    private static bool CoincideExtension(string extension, string mimeType) => mimeType switch
    {
        "image/jpeg" => extension is ".jpg" or ".jpeg",
        "image/png" => extension == ".png",
        "image/webp" => extension == ".webp",
        _ => false
    };

    /// <summary>
    /// Normaliza el Content-Type declarado por el navegador. Devuelve null si
    /// no viene informado (no se rechaza por eso) y un MIME canónico si es
    /// una imagen reconocible.
    /// </summary>
    private static string? NormalizarTipoDeclarado(string? contentType)
    {
        var valor = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        if (valor.Length == 0)
            return null;

        return valor switch
        {
            "image/jpeg" or "image/jpg" or "image/pjpeg" => "image/jpeg",
            "image/png" or "image/x-png" => "image/png",
            "image/webp" => "image/webp",
            _ => valor // tipo desconocido: se compara literal (se rechazará si no coincide)
        };
    }
}
