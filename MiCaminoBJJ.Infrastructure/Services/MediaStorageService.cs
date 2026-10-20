using Microsoft.Extensions.Logging;
using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;

namespace MiCaminoBJJ.Infrastructure.Services;

public class MediaStorageService : IMediaStorageService
{
    private readonly ILogger<MediaStorageService> _logger;
    private readonly string _basePath;
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    public MediaStorageService(ILogger<MediaStorageService> logger)
    {
        _logger = logger;
        _basePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MiCaminoBJJ");
    }

    private static readonly HashSet<string> AllowedImageMimes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif"
    };

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private static readonly HashSet<string> AllowedVideoMimes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4", "video/webm", "video/quicktime"
    };

    private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm", ".mov"
    };

    private static readonly HashSet<string> AllowedDocumentMimes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    private static readonly HashSet<string> AllowedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx"
    };

    public async Task<string> SaveFileAsync(Stream fileStream, string originalFileName, MediaType mediaType,
        MediaSourceType sourceType, int userId, int? techniqueId = null)
    {
        var extension = Path.GetExtension(originalFileName);
        var uniqueName = $"{Guid.NewGuid()}{extension}";

        string relativePath;
        if (sourceType == MediaSourceType.PackagedResource)
        {
            relativePath = Path.Combine("media", "official", "techniques", uniqueName);
        }
        else if (techniqueId.HasValue)
        {
            relativePath = Path.Combine("media", "users", userId.ToString(), "techniques", uniqueName);
        }
        else
        {
            relativePath = Path.Combine("media", "users", userId.ToString(), "profile", uniqueName);
        }

        var basePath = GetBasePath();
        var fullPath = Path.Combine(basePath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var outputStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await fileStream.CopyToAsync(outputStream);

        _logger.LogDebug("File saved: {Path}", relativePath);
        return relativePath;
    }

    public Task DeleteFileAsync(string relativePath)
    {
        var fullPath = Path.Combine(GetBasePath(), relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogDebug("File deleted: {Path}", relativePath);
        }
        return Task.CompletedTask;
    }

    public Task<string?> GetFullPathAsync(string relativePath)
    {
        var fullPath = Path.Combine(GetBasePath(), relativePath);
        return Task.FromResult(File.Exists(fullPath) ? fullPath : null);
    }

    public Task<bool> FileExistsAsync(string relativePath)
    {
        var fullPath = Path.Combine(GetBasePath(), relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public FileValidationResult ValidateFile(string fileName, long fileSizeBytes, Stream? fileStream = null)
    {
        var result = new FileValidationResult { IsValid = true };

        // Check size
        if (fileSizeBytes > MaxFileSizeBytes)
        {
            result.IsValid = false;
            result.ErrorMessage = $"El archivo supera el tamaño máximo de {MaxFileSizeBytes / (1024 * 1024)} MB.";
            return result;
        }

        result.FileSizeBytes = fileSizeBytes;

        // Check extension
        var extension = Path.GetExtension(fileName);
        if (AllowedImageExtensions.Contains(extension))
        {
            result.MimeType = extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };
        }
        else if (AllowedVideoExtensions.Contains(extension))
        {
            result.MimeType = extension.ToLowerInvariant() switch
            {
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mov" => "video/quicktime",
                _ => "application/octet-stream"
            };
        }
        else if (AllowedDocumentExtensions.Contains(extension))
        {
            result.MimeType = extension.ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                _ => "application/octet-stream"
            };
        }
        else
        {
            result.IsValid = false;
            result.ErrorMessage = "Formato de archivo no soportado. Extensiones permitidas: .jpg, .png, .webp, .gif, .mp4, .webm, .mov, .pdf, .doc, .docx.";
            return result;
        }

        return result;
    }

    private string GetBasePath() => _basePath;
}
