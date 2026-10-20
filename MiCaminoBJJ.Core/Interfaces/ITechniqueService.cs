using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Core.Interfaces;

public interface ITechniqueService
{
    Task<Technique?> GetByIdAsync(int techniqueId);
    Task<List<Technique>> GetAllAsync();
    Task<List<Technique>> GetAllForAdminAsync();
    Task<List<Technique>> GetByBeltIdAsync(int beltId);
    Task<List<Technique>> GetByCategoryIdAsync(int categoryId);
    Task<List<Technique>> GetByPositionIdAsync(int positionId);
    Task<Technique?> GetLastViewedByUserAsync(int userId);
    Task<Technique> CreateAsync(string name, string description, string objective, string? steps, string? tips, int beltId, int categoryId, int positionId);
    Task UpdateAsync(int techniqueId, string name, string description, string objective, string? steps, string? tips, int beltId, int categoryId, int positionId);
    Task SetActiveAsync(int techniqueId, bool isActive);
}
