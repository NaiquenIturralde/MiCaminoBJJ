using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Entities;

public class TrainingSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public TrainingType TrainingType { get; set; }
    public DateTime Date { get; set; }
    public int DurationMinutes { get; set; }
    public Feeling Feeling { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<TechniquePractice> Practices { get; set; } = new List<TechniquePractice>();
}
