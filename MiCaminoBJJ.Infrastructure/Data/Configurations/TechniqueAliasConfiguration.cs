using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class TechniqueAliasConfiguration : IEntityTypeConfiguration<TechniqueAlias>
{
    public void Configure(EntityTypeBuilder<TechniqueAlias> builder)
    {
        builder.ToTable("TechniqueAliases");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.NameNormalized)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasOne(e => e.Technique)
            .WithMany(t => t.Aliases)
            .HasForeignKey(e => e.TechniqueId)
            .OnDelete(DeleteBehavior.Cascade);

        // Anti-duplicados: "América" y "AMERICA" colisionan normalizados dentro de la misma técnica.
        builder.HasIndex(e => new { e.TechniqueId, e.NameNormalized })
            .IsUnique()
            .HasDatabaseName("UX_TechniqueAliases_TechniqueId_NameNormalized");

        // Búsqueda por sinónimo (subcadena; el índice cubre igualmente igualdad/prefijo).
        builder.HasIndex(e => e.NameNormalized)
            .HasDatabaseName("IX_TechniqueAliases_NameNormalized");
    }
}
