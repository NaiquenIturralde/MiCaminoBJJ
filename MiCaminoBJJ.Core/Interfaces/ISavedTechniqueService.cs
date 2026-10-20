using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Core.Interfaces;

public interface ISavedTechniqueService
{
    Task<List<SavedTechnique>> GetByUserAsync(int userId);
    Task<bool> IsSavedAsync(int userId, int techniqueId);
    Task<SavedTechnique?> GetSavedAsync(int userId, int techniqueId);
    Task<SavedTechnique> SaveAsync(int userId, int techniqueId, Enums.SavedType savedType);
    Task ChangeTypeAsync(int savedId, Enums.SavedType newType);
    Task RemoveAsync(int savedId);
    Task<int> GetFavoriteCountAsync(int userId);
    Task<int> GetWatchLaterCountAsync(int userId);
}
