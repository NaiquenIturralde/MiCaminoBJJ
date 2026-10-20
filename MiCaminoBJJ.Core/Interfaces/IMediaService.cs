using MiCaminoBJJ.Core.Entities;

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
    Task RemoveOfficialMediaAsync(int techniqueId, int mediaAssetId);
    Task SetCoverAsync(int techniqueId, int mediaAssetId);
    Task<UserTechniqueResource> AddPersonalResourceAsync(int userId, int techniqueId, int mediaAssetId, string? notes, int displayOrder);
    Task<UserProfileMedia> AddProfileMediaAsync(int userId, int mediaAssetId, bool isProfilePicture);
    Task SetProfilePictureAsync(int userId, int mediaAssetId);
}
