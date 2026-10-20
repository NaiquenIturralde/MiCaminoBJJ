namespace MiCaminoBJJ.Core.Entities;

public class UserProfileMedia
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int MediaAssetId { get; set; }
    public bool IsProfilePicture { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public MediaAsset MediaAsset { get; set; } = null!;
}
