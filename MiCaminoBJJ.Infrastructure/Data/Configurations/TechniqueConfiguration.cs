using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class TechniqueConfiguration : IEntityTypeConfiguration<Technique>
{
    public void Configure(EntityTypeBuilder<Technique> builder)
    {
        builder.ToTable("Techniques");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        // Normalizado para búsqueda sin tildes/mayúsculas. Nullable: las filas
        // existentes se rellenan con backfill idempotente al iniciar la app.
        builder.Property(e => e.NameNormalized)
            .HasMaxLength(200);

        builder.HasMany(e => e.Aliases)
            .WithOne(a => a.Technique)
            .HasForeignKey(a => a.TechniqueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.Objective)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.Steps)
            .HasMaxLength(4000);

        builder.Property(e => e.Tips)
            .HasMaxLength(4000);

        builder.HasOne(e => e.Belt)
            .WithMany(e => e.Techniques)
            .HasForeignKey(e => e.BeltId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Category)
            .WithMany(e => e.Techniques)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Position)
            .WithMany(e => e.Techniques)
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => e.BeltId);
        builder.HasIndex(e => e.CategoryId);
        builder.HasIndex(e => e.PositionId);
    }
}
