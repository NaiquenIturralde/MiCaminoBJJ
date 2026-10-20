using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Interfaces;

public class FileValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? MimeType { get; set; }
    public long FileSizeBytes { get; set; }
}

public interface IMediaStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string originalFileName, MediaType mediaType, MediaSourceType sourceType, int userId, int? techniqueId = null);
    Task DeleteFileAsync(string relativePath);
    Task<string?> GetFullPathAsync(string relativePath);
    Task<bool> FileExistsAsync(string relativePath);
    FileValidationResult ValidateFile(string fileName, long fileSizeBytes, Stream? fileStream = null);
}
