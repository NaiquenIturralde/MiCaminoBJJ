using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Entities;

public class TechniquePractice
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TechniqueId { get; set; }
    public int? TrainingSessionId { get; set; }
    public DateTime PracticedAt { get; set; } = DateTime.UtcNow;
    public int Repetitions { get; set; }
    public int DurationMinutes { get; set; }
    public ConfidenceLevel ConfidenceLevel { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Technique Technique { get; set; } = null!;
    public TrainingSession? TrainingSession { get; set; }
}
