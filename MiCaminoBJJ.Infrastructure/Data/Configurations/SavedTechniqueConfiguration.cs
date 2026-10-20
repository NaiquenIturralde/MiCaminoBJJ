using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class SavedTechniqueConfiguration : IEntityTypeConfiguration<SavedTechnique>
{
    public void Configure(EntityTypeBuilder<SavedTechnique> builder)
    {
        builder.ToTable("SavedTechniques");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.SavedType)
            .IsRequired()
            .HasConversion<int>();

        builder.HasOne(e => e.User)
            .WithMany(e => e.SavedTechniques)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Technique)
            .WithMany(e => e.SavedByUsers)
            .HasForeignKey(e => e.TechniqueId)
            .OnDelete(DeleteBehavior.Cascade);

        // Prevent duplicate saves
        builder.HasIndex(e => new { e.UserId, e.TechniqueId })
            .IsUnique();
    }
}
