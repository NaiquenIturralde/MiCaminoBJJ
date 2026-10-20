namespace MiCaminoBJJ.Core.Entities;

public class UserTournament
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Placement { get; set; }
    public string? Division { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
