namespace MiCaminoBJJ.Core.Entities;

public class UserSettings
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string? Theme { get; set; }
    public string? Language { get; set; }
    public bool NotificationsEnabled { get; set; } = true;

    public User User { get; set; } = null!;
}
