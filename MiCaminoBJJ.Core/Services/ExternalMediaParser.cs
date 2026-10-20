using System.Text.RegularExpressions;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;

namespace MiCaminoBJJ.Core.Services;

public partial class ExternalMediaParser : IExternalMediaParser
{
    public ParsedExternalMedia ParseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new ParsedExternalMedia
            {
                IsValid = false,
                ErrorMessage = "La URL no puede estar vacía."
            };
        }

        var trimmedUrl = url.Trim();

        // YouTube patterns
        var youtubeMatch = YouTubeRegex().Match(trimmedUrl);
        if (youtubeMatch.Success)
        {
            var videoId = youtubeMatch.Groups[1].Value;
            return new ParsedExternalMedia
            {
                IsValid = true,
                Provider = ExternalMediaProvider.YouTube,
                VideoId = videoId,
                OriginalUrl = trimmedUrl,
                EmbedUrl = $"https://www.youtube.com/embed/{videoId}"
            };
        }

        // Vimeo patterns
        var vimeoMatch = VimeoRegex().Match(trimmedUrl);
        if (vimeoMatch.Success)
        {
            var videoId = vimeoMatch.Groups[1].Value;
            return new ParsedExternalMedia
            {
                IsValid = true,
                Provider = ExternalMediaProvider.Vimeo,
                VideoId = videoId,
                OriginalUrl = trimmedUrl,
                EmbedUrl = $"https://player.vimeo.com/video/{videoId}"
            };
        }

        return new ParsedExternalMedia
        {
            IsValid = false,
            ErrorMessage = "La URL no corresponde a un proveedor de video soportado."
        };
    }

    [GeneratedRegex(@"(?:youtube\.com/watch\?v=|youtu\.be/|youtube\.com/shorts/|youtube\.com/embed/)([a-zA-Z0-9_-]{11})", RegexOptions.IgnoreCase)]
    private static partial Regex YouTubeRegex();

    [GeneratedRegex(@"vimeo\.com/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VimeoRegex();
}
