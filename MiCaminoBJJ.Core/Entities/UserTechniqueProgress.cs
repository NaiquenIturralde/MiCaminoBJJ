using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Entities;

public class UserTechniqueProgress
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TechniqueId { get; set; }
    public ProgressState ProgressState { get; set; } = ProgressState.NoIniciada;
    public DateTime? DominatedAt { get; set; }

    public User User { get; set; } = null!;
    public Technique Technique { get; set; } = null!;
}
