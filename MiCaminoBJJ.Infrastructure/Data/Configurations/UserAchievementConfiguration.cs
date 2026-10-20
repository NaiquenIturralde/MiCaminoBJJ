using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class UserAchievementConfiguration : IEntityTypeConfiguration<UserAchievement>
{
    public void Configure(EntityTypeBuilder<UserAchievement> builder)
    {
        builder.ToTable("UserAchievements");

        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.User)
            .WithMany(e => e.Achievements)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Achievement)
            .WithMany(e => e.UserAchievements)
            .HasForeignKey(e => e.AchievementId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent duplicate achievement awards
        builder.HasIndex(e => new { e.UserId, e.AchievementId })
            .IsUnique();
    }
}
