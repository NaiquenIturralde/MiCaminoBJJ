using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Entities;

public class MediaAsset
{
    public int Id { get; set; }
    public int UploadedByUserId { get; set; }
    public MediaType MediaType { get; set; }
    public MediaSourceType SourceType { get; set; }
    public string? LocalPath { get; set; }
    public string? RemoteUrl { get; set; }
    public string? ExternalUrl { get; set; }
    public ExternalMediaProvider ExternalProvider { get; set; }
    public string? ExternalVideoId { get; set; }
    public string? OriginalFileName { get; set; }
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailPath { get; set; }
    public SyncStatus SyncStatus { get; set; } = SyncStatus.LocalOnly;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    public User UploadedBy { get; set; } = null!;
    public ICollection<TechniqueMedia> TechniqueMedia { get; set; } = new List<TechniqueMedia>();
    public ICollection<UserTechniqueResource> UserResources { get; set; } = new List<UserTechniqueResource>();
    public ICollection<UserProfileMedia> ProfileMedia { get; set; } = new List<UserProfileMedia>();
}
