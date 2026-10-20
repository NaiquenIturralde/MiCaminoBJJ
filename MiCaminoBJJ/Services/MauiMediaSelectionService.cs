using MiCaminoBJJ.Core.Interfaces;

namespace MiCaminoBJJ.Services;

/// <summary>
/// MAUI implementation of IMediaSelectionService using FilePicker and MediaPicker.
/// </summary>
public class MauiMediaSelectionService : IMediaSelectionService
{
    public async Task<MediaSelectionResult?> PickPhotoAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Seleccionar imagen",
                FileTypes = FilePickerFileType.Images
            });

            return await MapResultAsync(result);
        }
        catch
        {
            return null;
        }
    }

    public async Task<MediaSelectionResult?> PickVideoAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Seleccionar video",
                FileTypes = FilePickerFileType.Videos
            });

            return await MapResultAsync(result);
        }
        catch
        {
            return null;
        }
    }

    public async Task<MediaSelectionResult?> PickFileAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Seleccionar archivo"
            });

            return await MapResultAsync(result);
        }
        catch
        {
            return null;
        }
    }

    public async Task<MediaSelectionResult?> CapturePhotoAsync()
    {
        try
        {
            var result = await MediaPicker.Default.CapturePhotoAsync();
            if (result == null)
                return new MediaSelectionResult { IsCancelled = true };

            var stream = await result.OpenReadAsync();
            var fileName = result.FileName;
            return new MediaSelectionResult
            {
                FileStream = stream,
                FileName = fileName,
                MimeType = GetMimeTypeFromFileName(fileName),
                FileSizeBytes = stream.CanSeek ? stream.Length : 0,
                IsCancelled = false
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<MediaSelectionResult?> CaptureVideoAsync()
    {
        try
        {
            var result = await MediaPicker.Default.CaptureVideoAsync();
            if (result == null)
                return new MediaSelectionResult { IsCancelled = true };

            var stream = await result.OpenReadAsync();
            var fileName = result.FileName;
            return new MediaSelectionResult
            {
                FileStream = stream,
                FileName = fileName,
                MimeType = GetMimeTypeFromFileName(fileName),
                FileSizeBytes = stream.CanSeek ? stream.Length : 0,
                IsCancelled = false
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<MediaSelectionResult>> PickMultiplePhotosAsync()
    {
        return new List<MediaSelectionResult>();
    }

    private async Task<MediaSelectionResult?> MapResultAsync(FileResult? result)
    {
        if (result == null)
            return new MediaSelectionResult { IsCancelled = true };

        var stream = await result.OpenReadAsync();
        return new MediaSelectionResult
        {
            FileStream = stream,
            FileName = result.FileName,
            MimeType = result.ContentType ?? GetMimeTypeFromFileName(result.FileName),
            FileSizeBytes = stream.CanSeek ? stream.Length : 0,
            IsCancelled = false
        };
    }

    private static string GetMimeTypeFromFileName(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
    }
}
