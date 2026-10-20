using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Core.Interfaces;
using MiCaminoBJJ.Infrastructure.Data;

namespace MiCaminoBJJ.Infrastructure.Services;

public class StatisticsService : IStatisticsService
{
    private readonly AppDbContext _context;

    public StatisticsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserStatistics> GetByUserAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var weekStart = now.AddDays(-(int)now.DayOfWeek);

        // Total training sessions
        var totalTrainings = await _context.TrainingSessions
            .CountAsync(ts => ts.UserId == userId);

        // Unique techniques practiced
        var totalPracticedTechniques = await _context.TechniquePractices
            .Where(tp => tp.UserId == userId)
            .Select(tp => tp.TechniqueId)
            .Distinct()
            .CountAsync();

        // Saved techniques count
        var totalSavedTechniques = await _context.SavedTechniques
            .CountAsync(st => st.UserId == userId);

        // Tournaments
        var totalTournaments = await _context.UserTournaments
            .CountAsync(ut => ut.UserId == userId);

        // Achievements
        var totalAchievements = await _context.UserAchievements
            .CountAsync(ua => ua.UserId == userId);

        // Last class date
        var lastClass = await _context.TrainingSessions
            .Where(ts => ts.UserId == userId)
            .OrderByDescending(ts => ts.Date)
            .Select(ts => (DateTime?)ts.Date)
            .FirstOrDefaultAsync();

        // Most viewed technique this week
        var mostViewed = await _context.TechniqueViews
            .Where(tv => tv.UserId == userId && tv.ViewedAt >= weekStart)
            .GroupBy(tv => tv.TechniqueId)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync();

        string? mostViewedName = null;
        if (mostViewed > 0)
        {
            var technique = await _context.Techniques.FindAsync(mostViewed);
            mostViewedName = technique?.Name;
        }

        return new UserStatistics
        {
            TotalTrainings = totalTrainings,
            TotalPracticedTechniques = totalPracticedTechniques,
            TotalSavedTechniques = totalSavedTechniques,
            TotalTournaments = totalTournaments,
            TotalAchievements = totalAchievements,
            LastClassDate = lastClass,
            MostViewedTechniqueThisWeek = mostViewedName
        };
    }
}
