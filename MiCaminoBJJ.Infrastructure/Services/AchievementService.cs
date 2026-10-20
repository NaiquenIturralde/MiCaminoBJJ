using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

public class AchievementService : IAchievementService
{
    private readonly AppDbContext _context;
    private readonly IStatisticsService _statisticsService;
    private readonly ITechniquePracticeService _practiceService;
    private readonly ILogger<AchievementService> _logger;

    public AchievementService(
        AppDbContext context,
        IStatisticsService statisticsService,
        ITechniquePracticeService practiceService,
        ILogger<AchievementService> logger)
    {
        _context = context;
        _statisticsService = statisticsService;
        _practiceService = practiceService;
        _logger = logger;
    }

    public async Task EvaluateAndAwardAsync(int userId)
    {
        var stats = await _statisticsService.GetByUserAsync(userId);
        var achievements = await _context.Achievements.ToListAsync();
        var userAchievements = await _context.UserAchievements
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.AchievementId)
            .ToListAsync();

        foreach (var achievement in achievements)
        {
            // Skip if already awarded and not repeatable
            if (userAchievements.Contains(achievement.Id) && !achievement.IsRepeatable)
                continue;

            bool earned = achievement.CriteriaType switch
            {
                "TrainingCount" => stats.TotalTrainings >= int.Parse(achievement.CriteriaValue),
                "TechniquePracticeCount" => stats.TotalPracticedTechniques >= int.Parse(achievement.CriteriaValue),
                "SavedTechniqueCount" => stats.TotalSavedTechniques >= int.Parse(achievement.CriteriaValue),
                "FirstTraining" => stats.TotalTrainings >= 1,
                "FirstPractice" => stats.TotalPracticedTechniques >= 1,
                _ => false
            };

            if (earned && !userAchievements.Contains(achievement.Id))
            {
                _context.UserAchievements.Add(new UserAchievement
                {
                    UserId = userId,
                    AchievementId = achievement.Id,
                    AwardedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                _logger.LogInformation("Achievement '{Name}' awarded to user {UserId}", achievement.Name, userId);
            }
        }
    }

    public async Task<List<UserAchievement>> GetByUserAsync(int userId)
    {
        return await _context.UserAchievements
            .Include(ua => ua.Achievement)
            .Where(ua => ua.UserId == userId)
            .OrderByDescending(ua => ua.AwardedAt)
            .ToListAsync();
    }

    public async Task<int> GetCountByUserAsync(int userId)
    {
        return await _context.UserAchievements
            .CountAsync(ua => ua.UserId == userId);
    }
}
