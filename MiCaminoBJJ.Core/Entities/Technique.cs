namespace MiCaminoBJJ.Core.Entities;

public class Technique
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string? Steps { get; set; }
    public string? Tips { get; set; }
    public int BeltId { get; set; }
    public int CategoryId { get; set; }
    public int PositionId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Belt Belt { get; set; } = null!;
    public TechniqueCategory Category { get; set; } = null!;
    public Position Position { get; set; } = null!;
    public ICollection<TechniqueMedia> OfficialMedia { get; set; } = new List<TechniqueMedia>();
    public ICollection<SavedTechnique> SavedByUsers { get; set; } = new List<SavedTechnique>();
    public ICollection<TechniqueView> ViewedByUsers { get; set; } = new List<TechniqueView>();
    public ICollection<TechniquePractice> Practices { get; set; } = new List<TechniquePractice>();
    public ICollection<UserTechniqueProgress> UserProgresses { get; set; } = new List<UserTechniqueProgress>();
    public ICollection<UserTechniqueResource> PersonalResources { get; set; } = new List<UserTechniqueResource>();
}
