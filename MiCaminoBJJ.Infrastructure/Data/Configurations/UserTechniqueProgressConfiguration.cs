using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class UserTechniqueProgressConfiguration : IEntityTypeConfiguration<UserTechniqueProgress>
{
    public void Configure(EntityTypeBuilder<UserTechniqueProgress> builder)
    {
        builder.ToTable("UserTechniqueProgress");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ProgressState)
            .IsRequired()
            .HasConversion<int>();

        builder.HasOne(e => e.User)
            .WithMany(e => e.TechniqueProgresses)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Technique)
            .WithMany(e => e.UserProgresses)
            .HasForeignKey(e => e.TechniqueId)
            .OnDelete(DeleteBehavior.Cascade);

        // One progress record per user-technique pair
        builder.HasIndex(e => new { e.UserId, e.TechniqueId })
            .IsUnique();
    }
}
