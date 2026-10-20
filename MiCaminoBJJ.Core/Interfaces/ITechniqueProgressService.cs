using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Interfaces;

public interface ITechniqueProgressService
{
    Task<UserTechniqueProgress?> GetAsync(int userId, int techniqueId);
    Task<UserTechniqueProgress> GetOrCreateAsync(int userId, int techniqueId);
    Task UpdateStateAsync(int userId, int techniqueId, ProgressState newState);
    Task MarkAsDominatedAsync(int userId, int techniqueId);
    Task<double> CalculatePercentageAsync(int userId, int techniqueId);
    Task<List<UserTechniqueProgress>> GetByUserAsync(int userId);
}
