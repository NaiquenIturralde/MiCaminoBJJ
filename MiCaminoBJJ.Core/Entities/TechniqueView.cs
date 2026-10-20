namespace MiCaminoBJJ.Core.Entities;

public class TechniqueView
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TechniqueId { get; set; }
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Technique Technique { get; set; } = null!;
}
