using MiCaminoBJJ.Core.Enums;

namespace MiCaminoBJJ.Core.Entities;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string BeltRank { get; set; } = string.Empty;
    public string? Academy { get; set; }
    public UserRole Role { get; set; } = UserRole.Practicante;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TrainingSession> TrainingSessions { get; set; } = new List<TrainingSession>();
    public ICollection<SavedTechnique> SavedTechniques { get; set; } = new List<SavedTechnique>();
    public ICollection<TechniqueView> TechniqueViews { get; set; } = new List<TechniqueView>();
    public ICollection<TechniquePractice> TechniquePractices { get; set; } = new List<TechniquePractice>();
    public ICollection<UserTechniqueProgress> TechniqueProgresses { get; set; } = new List<UserTechniqueProgress>();
    public ICollection<UserAchievement> Achievements { get; set; } = new List<UserAchievement>();
    public ICollection<UserTournament> Tournaments { get; set; } = new List<UserTournament>();
    public UserSettings? Settings { get; set; }
    public ICollection<UserTechniqueResource> PersonalResources { get; set; } = new List<UserTechniqueResource>();
    public ICollection<UserProfileMedia> ProfileMedia { get; set; } = new List<UserProfileMedia>();
    public ICollection<MediaAsset> UploadedMedia { get; set; } = new List<MediaAsset>();
}
