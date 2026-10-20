using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("UserSettings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Theme)
            .HasMaxLength(20);

        builder.Property(e => e.Language)
            .HasMaxLength(10);
    }
}
