using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Core.Interfaces;

public interface IAchievementService
{
    Task EvaluateAndAwardAsync(int userId);
    Task<List<UserAchievement>> GetByUserAsync(int userId);
    Task<int> GetCountByUserAsync(int userId);
}
