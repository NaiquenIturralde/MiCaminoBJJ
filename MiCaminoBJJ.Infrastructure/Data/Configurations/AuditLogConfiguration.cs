using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(e => e.Id);

        // UserId SIN clave foránea a propósito: la auditoría debe sobrevivir
        // a la purga del usuario. El índice permite consultar por actor.
        builder.Property(e => e.UserId);

        builder.Property(e => e.Action)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.EntityId)
            .IsRequired();

        builder.Property(e => e.TimestampUtc)
            .IsRequired();

        builder.Property(e => e.Result)
            .IsRequired()
            .HasConversion<int>();

        // JSON de cambios con lista blanca; solo lectura legal, nunca escritura
        // desde la interfaz de usuario.
        builder.Property(e => e.Details);

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(500);

        // Consultas típicas: "historial de esta entidad" y "quién hizo qué".
        builder.HasIndex(e => new { e.EntityType, e.EntityId });
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.TimestampUtc);
    }
}
