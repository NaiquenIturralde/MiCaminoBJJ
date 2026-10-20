using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class BeltConfiguration : IEntityTypeConfiguration<Belt>
{
    public void Configure(EntityTypeBuilder<Belt> builder)
    {
        builder.ToTable("Belts");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(e => e.DisplayOrder)
            .IsUnique();

        builder.Property(e => e.ImagePath)
            .HasMaxLength(500);

        builder.Property(e => e.Description)
            .HasMaxLength(500);
    }
}
