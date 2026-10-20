namespace MiCaminoBJJ.Core.Interfaces;

public interface IMediaSelectionService
{
    Task<MediaSelectionResult?> PickPhotoAsync();
    Task<MediaSelectionResult?> PickVideoAsync();
    Task<MediaSelectionResult?> PickFileAsync();
    Task<MediaSelectionResult?> CapturePhotoAsync();
    Task<MediaSelectionResult?> CaptureVideoAsync();
    Task<List<MediaSelectionResult>> PickMultiplePhotosAsync();
}

public class MediaSelectionResult
{
    public Stream? FileStream { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public bool IsCancelled { get; set; }
}
