using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Interfaces;

public class TrainingWithTechniques
{
    public TrainingType TrainingType { get; set; }
    public DateTime Date { get; set; }

    /// <summary>
    /// Duración total de la sesión en minutos. Es un campo independiente de la suma de
    /// <see cref="TechniquePracticeInput.DurationMinutes"/>: las técnicas pueden solaparse
    /// o incluir actividades simultáneas, por lo que la suma puede ser mayor, menor o igual
    /// a la duración total. Solo se validan los rangos individuales de cada campo.
    /// </summary>
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
    /// <summary>
    /// Crea una sesión de entrenamiento con sus técnicas de forma transaccional.
    /// Valida fecha, duración, repeticiones, minutos por técnica, técnicas duplicadas
    /// y que todas las técnicas existan y estén activas; si algo falla, no se persiste nada.
    /// </summary>
    Task<TrainingSession> CreateAsync(int userId, TrainingWithTechniques input);

    Task<List<TrainingSession>> GetByUserAsync(int userId);

    /// <summary>
    /// Obtiene una sesión por identificador solo si pertenece al usuario indicado.
    /// Retorna null si la sesión no existe o pertenece a otro usuario.
    /// </summary>
    Task<TrainingSession?> GetByIdAsync(int userId, int sessionId);
    Task<int> GetTotalCountAsync(int userId);
    Task<TrainingSession?> GetLastAsync(int userId);
}
