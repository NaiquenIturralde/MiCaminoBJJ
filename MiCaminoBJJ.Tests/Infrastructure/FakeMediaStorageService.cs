using MiCaminoBJJ.Core.Enums;
using MiCaminoBJJ.Core.Interfaces;

namespace MiCaminoBJJ.Tests.Infrastructure;

/// <summary>
/// IMediaStorageService en memoria (Etapa 1.2.1): registra archivos guardados
/// y borrados para poder detectar huérfanos, borrados indebidos de archivos
/// compartidos y el orden autorización → almacenamiento, sin tocar el disco.
/// </summary>
public sealed class FakeMediaStorageService : IMediaStorageService
{
    private static readonly string[] AllowedExtensions =
        { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".mp4", ".webm", ".mov", ".pdf", ".doc", ".docx" };

    /// <summary>Archivos "en disco": ruta relativa → contenido.</summary>
    public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int SaveCalls { get; private set; }
    public int DeleteCalls { get; private set; }

    public Task<string> SaveFileAsync(Stream fileStream, string originalFileName, MediaType mediaType, MediaSourceType sourceType, int userId, int? techniqueId = null)
    {
        var relativePath = $"media/official/techniques/{Guid.NewGuid():N}{Path.GetExtension(originalFileName)}";
        Files[relativePath] = $"{originalFileName}|{userId}|{techniqueId?.ToString() ?? "-"}";
        SaveCalls++;
        return Task.FromResult(relativePath);
    }

    public Task DeleteFileAsync(string relativePath)
    {
        Files.Remove(relativePath);
        DeleteCalls++;
        return Task.CompletedTask;
    }

    public Task<string?> GetFullPathAsync(string relativePath) =>
        Task.FromResult(Files.ContainsKey(relativePath) ? relativePath : null);

    public Task<bool> FileExistsAsync(string relativePath) =>
        Task.FromResult(Files.ContainsKey(relativePath));

    public FileValidationResult ValidateFile(string fileName, long fileSizeBytes, Stream? fileStream = null)
    {
        if (!AllowedExtensions.Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase))
            return new FileValidationResult { IsValid = false, ErrorMessage = "Formato de archivo no soportado." };

        return new FileValidationResult { IsValid = true, MimeType = "image/jpeg", FileSizeBytes = fileSizeBytes };
    }

    /// <summary>Simula un archivo preexistente en el almacén.</summary>
    public void AddExistingFile(string relativePath, string content = "contenido") =>
        Files[relativePath] = content;
}
