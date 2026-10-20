namespace MiCaminoBJJ.Core.Entities;

public class TechniqueMedia
{
    public int Id { get; set; }
    public int TechniqueId { get; set; }
    public int MediaAssetId { get; set; }
    public bool IsCover { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Technique Technique { get; set; } = null!;
    public MediaAsset MediaAsset { get; set; } = null!;
}
