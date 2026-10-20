using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class TechniquePracticeConfiguration : IEntityTypeConfiguration<TechniquePractice>
{
    public void Configure(EntityTypeBuilder<TechniquePractice> builder)
    {
        builder.ToTable("TechniquePractices");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ConfidenceLevel)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.Notes)
            .HasMaxLength(1000);

        builder.HasOne(e => e.User)
            .WithMany(e => e.TechniquePractices)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Technique)
            .WithMany(e => e.Practices)
            .HasForeignKey(e => e.TechniqueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.TrainingSession)
            .WithMany(e => e.Practices)
            .HasForeignKey(e => e.TrainingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserId, e.TechniqueId });
        builder.HasIndex(e => e.TrainingSessionId);
        builder.HasIndex(e => e.PracticedAt);
    }
}
