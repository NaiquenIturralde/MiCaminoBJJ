using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class UserTechniqueResourceConfiguration : IEntityTypeConfiguration<UserTechniqueResource>
{
    public void Configure(EntityTypeBuilder<UserTechniqueResource> builder)
    {
        builder.ToTable("UserTechniqueResources");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.PersonalNotes)
            .HasMaxLength(1000);

        builder.HasOne(e => e.User)
            .WithMany(e => e.PersonalResources)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Technique)
            .WithMany(e => e.PersonalResources)
            .HasForeignKey(e => e.TechniqueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.MediaAsset)
            .WithMany(e => e.UserResources)
            .HasForeignKey(e => e.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.UserId, e.MediaAssetId });
    }
}
