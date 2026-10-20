using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class TechniqueViewConfiguration : IEntityTypeConfiguration<TechniqueView>
{
    public void Configure(EntityTypeBuilder<TechniqueView> builder)
    {
        builder.ToTable("TechniqueViews");

        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.User)
            .WithMany(e => e.TechniqueViews)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Technique)
            .WithMany(e => e.ViewedByUsers)
            .HasForeignKey(e => e.TechniqueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ViewedAt);
        builder.HasIndex(e => new { e.UserId, e.TechniqueId });
    }
}
