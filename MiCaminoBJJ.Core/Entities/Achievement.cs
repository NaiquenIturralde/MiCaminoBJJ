namespace MiCaminoBJJ.Core.Entities;

public class Achievement
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconPath { get; set; }
    public string CriteriaType { get; set; } = string.Empty;
    public string CriteriaValue { get; set; } = string.Empty;
    public bool IsRepeatable { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
