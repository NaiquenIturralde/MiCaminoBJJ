namespace MiCaminoBJJ.Core.Interfaces;

public class UserStatistics
{
    public int TotalTrainings { get; set; }
    public int TotalPracticedTechniques { get; set; }
    public int TotalSavedTechniques { get; set; }
    public int TotalTournaments { get; set; }
    public int TotalAchievements { get; set; }
    public DateTime? LastClassDate { get; set; }
    public string? MostViewedTechniqueThisWeek { get; set; }
}

public interface IStatisticsService
{
    Task<UserStatistics> GetByUserAsync(int userId);
}
