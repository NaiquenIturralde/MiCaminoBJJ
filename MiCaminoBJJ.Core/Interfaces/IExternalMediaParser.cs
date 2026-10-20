using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Interfaces;

public class ParsedExternalMedia
{
    public bool IsValid { get; set; }
    public ExternalMediaProvider Provider { get; set; }
    public string? VideoId { get; set; }
    public string? OriginalUrl { get; set; }
    public string? EmbedUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IExternalMediaParser
{
    ParsedExternalMedia ParseUrl(string url);
}
