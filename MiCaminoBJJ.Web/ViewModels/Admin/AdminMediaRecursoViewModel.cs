using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;

namespace MiCaminoBJJ.Web.ViewModels.Admin;

/// <summary>
/// Un recurso multimedia ASOCIADO a una técnica, tal como lo muestra la
/// sección administrativa «Multimedia de la técnica».
///
/// Expone lo necesario para:
/// - vista previa con reproductor integrado (YouTube/Vimeo vía
///   <see cref="IExternalMediaParser"/>, MISMO parser que la ficha pública);
/// - miniatura 16:9 de YouTube y YouTube Shorts (i.ytimg.com dentro del marco
///   <c>mccjrc-video-frame</c>, que ya fija aspect-ratio 16/9 en CSS);
/// - edición inline de URL, título y descripción (los campos vuelven a la
///   etiqueta con los valores actuales del recurso).
///
/// Los archivos locales (sin URL externa) se muestran como tarjeta simple: la
/// web todavía no sirve archivos, pero el administrador debe VERLOS para poder
/// desvincularlos.
/// </summary>
public sealed class AdminMediaRecursoViewModel
{
    /// <summary>Id de la asociación (<see cref="TechniqueMedia.Id"/>): es lo que edita y quita el formulario.</summary>
    public int MediaId { get; init; }

    /// <summary>Id del recurso subyacente (<see cref="MediaAsset.Id"/>), solo informativo.</summary>
    public int AssetId { get; init; }

    public string Titulo { get; init; } = string.Empty;

    public string? Descripcion { get; init; }

    /// <summary>URL almacenada (se precarga en el campo de edición). Vacía para archivos locales.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Enlace para «Abrir»: canónico de YouTube cuando corresponde
    /// (válido también para Shorts), si no la URL original.
    /// </summary>
    public string UrlCanonica { get; init; } = string.Empty;

    /// <summary>URL de embed; null cuando no hay vista previa reproducible.</summary>
    public string? EmbedUrl { get; init; }

    /// <summary>Miniatura de YouTube (i.ytimg.com); null si no hay miniatura disponible.</summary>
    public string? MiniaturaUrl { get; init; }

    /// <summary>Etiqueta del proveedor: «YouTube», «Vimeo», «Enlace externo» o «Archivo local».</summary>
    public string Proveedor { get; init; } = string.Empty;

    /// <summary>Nombre original del archivo cuando el recurso es local.</summary>
    public string? NombreArchivo { get; init; }

    public bool EsPortada { get; init; }

    public int DisplayOrder { get; init; }

    /// <summary>True cuando se muestra con el reproductor integrado (YouTube o Vimeo).</summary>
    public bool EsVideoIntegrado => !string.IsNullOrEmpty(EmbedUrl);

    /// <summary>True cuando el recurso no tiene enlace externo (archivo local sin servido en web).</summary>
    public bool EsArchivoLocal => string.IsNullOrEmpty(Url) && string.IsNullOrEmpty(EmbedUrl);

    /// <summary>
    /// Construye la vista de un vínculo usando el MISMO parser de la ficha
    /// pública: watch, youtu.be, shorts e embed generan reproductor + miniatura;
    /// el resto queda como enlace simple. Estado vacío o recurso borrado no
    /// llegan acá (los filtra el servicio de consultas).
    /// </summary>
    public static AdminMediaRecursoViewModel Desde(TechniqueMedia media, IExternalMediaParser parser)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(parser);

        var asset = media.MediaAsset;
        var url = asset?.ExternalUrl?.Trim() ?? string.Empty;
        var esYouTube = false;
        string? embed = null;
        string? miniatura = null;
        var proveedor = "Archivo local";
        var urlCanonica = url;

        if (!string.IsNullOrEmpty(url))
        {
            var parsed = parser.ParseUrl(url);

            if (parsed.IsValid && !string.IsNullOrWhiteSpace(parsed.EmbedUrl))
            {
                embed = parsed.EmbedUrl;
                proveedor = parsed.Provider == ExternalMediaProvider.Vimeo ? "Vimeo" : "YouTube";
            }
            else
            {
                proveedor = "Enlace externo";
            }

            esYouTube = parsed.IsValid
                && parsed.Provider == ExternalMediaProvider.YouTube
                && !string.IsNullOrWhiteSpace(parsed.VideoId);

            if (esYouTube)
            {
                // Misma miniatura que la ficha pública (válida también para Shorts);
                // el marco CSS la recorta a 16:9.
                miniatura = $"https://i.ytimg.com/vi/{parsed.VideoId}/hqdefault.jpg";
                urlCanonica = $"https://www.youtube.com/watch?v={parsed.VideoId}";
            }
        }

        return new AdminMediaRecursoViewModel
        {
            MediaId = media.Id,
            AssetId = asset?.Id ?? 0,
            Titulo = string.IsNullOrWhiteSpace(asset?.Title)
                ? (string.IsNullOrEmpty(url) ? asset?.OriginalFileName ?? "Recurso multimedia" : url)
                : asset!.Title!,
            Descripcion = asset?.Description,
            Url = url,
            UrlCanonica = urlCanonica,
            EmbedUrl = embed,
            MiniaturaUrl = miniatura,
            Proveedor = proveedor,
            NombreArchivo = asset?.OriginalFileName,
            EsPortada = media.IsCover,
            DisplayOrder = media.DisplayOrder
        };
    }
}
