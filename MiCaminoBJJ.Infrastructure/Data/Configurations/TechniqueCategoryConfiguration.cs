using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class TechniqueCategoryConfiguration : IEntityTypeConfiguration<TechniqueCategory>
{
    public void Configure(EntityTypeBuilder<TechniqueCategory> builder)
    {
        builder.ToTable("TechniqueCategories");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(e => e.DisplayOrder)
            .IsUnique();

        builder.Property(e => e.Description)
            .HasMaxLength(500);
    }
}
