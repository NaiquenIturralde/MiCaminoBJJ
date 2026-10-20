using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class TechniqueMediaConfiguration : IEntityTypeConfiguration<TechniqueMedia>
{
    public void Configure(EntityTypeBuilder<TechniqueMedia> builder)
    {
        builder.ToTable("TechniqueMedia");

        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.Technique)
            .WithMany(e => e.OfficialMedia)
            .HasForeignKey(e => e.TechniqueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.MediaAsset)
            .WithMany(e => e.TechniqueMedia)
            .HasForeignKey(e => e.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent duplicate associations
        builder.HasIndex(e => new { e.TechniqueId, e.MediaAssetId })
            .IsUnique();
    }
}
