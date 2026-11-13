using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Interfaces;

public interface IMediaService
{
    Task<MediaAsset> CreateAsync(MediaAsset mediaAsset);
    Task<MediaAsset?> GetByIdAsync(int mediaAssetId);
    Task SoftDeleteAsync(int mediaAssetId);
    Task<List<MediaAsset>> GetOfficialByTechniqueAsync(int techniqueId);
    Task<List<TechniqueMedia>> GetOfficialMediaEntriesAsync(int techniqueId);
    Task<List<MediaAsset>> GetPersonalByTechniqueAsync(int userId, int techniqueId);
    Task<TechniqueMedia> AddOfficialMediaAsync(int techniqueId, int mediaAssetId, bool isCover, int displayOrder);

    /// <summary>
    /// Alta administrativa ATÓMICA desde archivo local (Etapa 1.2.1).
    /// Orden garantizado: autorización → escritura del archivo → recurso +
    /// vínculo + UNA auditoría en la misma transacción → commit.
    /// Si la base falla después de guardar el archivo, el archivo se elimina
    /// (compensación) para no dejar huérfanos.
    /// </summary>
    Task<TechniqueMedia> AddOfficialMediaFromFileAsync(
        int techniqueId, Stream fileStream, string originalFileName, MediaType mediaType, bool isCover, int displayOrder);

    /// <summary>
    /// Alta administrativa ATÓMICA de un recurso ya construido (enlace externo):
    /// crea el recurso, lo vincula y audita una única vez en la misma transacción.
    /// </summary>
    Task<TechniqueMedia> AddOfficialMediaFromAssetAsync(
        MediaAsset mediaAsset, int techniqueId, bool isCover, int displayOrder);

    /// <summary>
    /// Baja administrativa ATÓMICA: desvincula de la técnica, aplica (o no) la
    /// eliminación lógica según queden referencias válidas, escribe UNA
    /// auditoría y, solo después del commit y sin referencias restantes,
    /// elimina físicamente el archivo. Nunca borra un archivo compartido.
    /// </summary>
    Task RemoveOfficialMediaAsync(int techniqueId, int mediaAssetId);
    Task SetCoverAsync(int techniqueId, int mediaAssetId);
    Task<UserTechniqueResource> AddPersonalResourceAsync(int userId, int techniqueId, int mediaAssetId, string? notes, int displayOrder);
    Task<UserProfileMedia> AddProfileMediaAsync(int userId, int mediaAssetId, bool isProfilePicture);
    Task SetProfilePictureAsync(int userId, int mediaAssetId);
}
