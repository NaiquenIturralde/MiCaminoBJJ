using MiCaminoBJJ.Web.Services.Recorrido;

namespace MiCaminoBJJ.Web.ViewModels.Recorrido;

/// <summary>
/// Datos del video listos para el reproductor con vista previa (Etapa 6A).
/// Todas las URLs derivan de un ID de YouTube validado por el parser
/// compartido; el iframe recién se construye en el navegador al presionar
/// Play (no hay autoplay en el HTML inicial).
/// </summary>
public sealed class VideoDemostrativoViewModel
{
    public string VideoId { get; init; } = string.Empty;
    public string Titulo { get; init; } = string.Empty;
    public string Nota { get; init; } = string.Empty;
    public string MiniaturaUrl { get; init; } = string.Empty;
    public string EmbedUrl { get; init; } = string.Empty;
    public string WatchUrl { get; init; } = string.Empty;

    public static VideoDemostrativoViewModel Desde(VideoRecorrido video) => new()
    {
        VideoId = video.VideoId,
        Titulo = video.Titulo,
        Nota = video.Nota,
        MiniaturaUrl = video.MiniaturaUrl,
        EmbedUrl = video.EmbedUrl,
        WatchUrl = video.WatchUrl
    };
}
