namespace MiCaminoBJJ.Core.Entities;

public class UserTechniqueResource
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int TechniqueId { get; set; }
    public int MediaAssetId { get; set; }
    public string? PersonalNotes { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Technique Technique { get; set; } = null!;
    public MediaAsset MediaAsset { get; set; } = null!;
}
