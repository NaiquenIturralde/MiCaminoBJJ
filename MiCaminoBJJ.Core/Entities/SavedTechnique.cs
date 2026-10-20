using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Entities;

public class SavedTechnique
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TechniqueId { get; set; }
    public SavedType SavedType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Technique Technique { get; set; } = null!;
}
