using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Interfaces;

public class TrainingWithTechniques
{
    public TrainingType TrainingType { get; set; }
    public DateTime Date { get; set; }
    public int DurationMinutes { get; set; }
    public Feeling Feeling { get; set; }
    public string? Notes { get; set; }
    public List<TechniquePracticeInput> Techniques { get; set; } = new();
}

public class TechniquePracticeInput
{
    public int TechniqueId { get; set; }
    public int Repetitions { get; set; }
    public int DurationMinutes { get; set; }
    public ConfidenceLevel Confidence { get; set; }
    public string? Notes { get; set; }
}

public interface ITrainingService
{
    Task<TrainingSession> CreateAsync(int userId, TrainingWithTechniques input);
    Task<List<TrainingSession>> GetByUserAsync(int userId);
    Task<TrainingSession?> GetByIdAsync(int sessionId);
    Task<int> GetTotalCountAsync(int userId);
    Task<TrainingSession?> GetLastAsync(int userId);
}
