using Microsoft.EntityFrameworkCore;
using MiCaminoBJJ.Core.Entities;
using MiCaminoBJJ.Infrastructure.Data.Configurations;

namespace MiCaminoBJJ.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Belt> Belts => Set<Belt>();
    public DbSet<TechniqueCategory> TechniqueCategories => Set<TechniqueCategory>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Technique> Techniques => Set<Technique>();
    public DbSet<TechniqueAlias> TechniqueAliases => Set<TechniqueAlias>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<TechniqueMedia> TechniqueMedia => Set<TechniqueMedia>();
    public DbSet<UserTechniqueResource> UserTechniqueResources => Set<UserTechniqueResource>();
    public DbSet<UserProfileMedia> UserProfileMedia => Set<UserProfileMedia>();
    public DbSet<SavedTechnique> SavedTechniques => Set<SavedTechnique>();
    public DbSet<TechniqueView> TechniqueViews => Set<TechniqueView>();
    public DbSet<TechniquePractice> TechniquePractices => Set<TechniquePractice>();
    public DbSet<UserTechniqueProgress> UserTechniqueProgress => Set<UserTechniqueProgress>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<UserTournament> UserTournaments => Set<UserTournament>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new BeltConfiguration());
        modelBuilder.ApplyConfiguration(new TechniqueCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new PositionConfiguration());
        modelBuilder.ApplyConfiguration(new TechniqueConfiguration());
        modelBuilder.ApplyConfiguration(new TechniqueAliasConfiguration());
        modelBuilder.ApplyConfiguration(new MediaAssetConfiguration());
        modelBuilder.ApplyConfiguration(new TechniqueMediaConfiguration());
        modelBuilder.ApplyConfiguration(new UserTechniqueResourceConfiguration());
        modelBuilder.ApplyConfiguration(new UserProfileMediaConfiguration());
        modelBuilder.ApplyConfiguration(new SavedTechniqueConfiguration());
        modelBuilder.ApplyConfiguration(new TechniqueViewConfiguration());
        modelBuilder.ApplyConfiguration(new TechniquePracticeConfiguration());
        modelBuilder.ApplyConfiguration(new UserTechniqueProgressConfiguration());
        modelBuilder.ApplyConfiguration(new TrainingSessionConfiguration());
        modelBuilder.ApplyConfiguration(new AchievementConfiguration());
        modelBuilder.ApplyConfiguration(new UserAchievementConfiguration());
        modelBuilder.ApplyConfiguration(new UserTournamentConfiguration());
        modelBuilder.ApplyConfiguration(new UserSettingsConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
    }
}
