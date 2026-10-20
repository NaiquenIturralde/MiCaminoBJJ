using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(e => e.Email)
            .IsUnique();

        builder.Property(e => e.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.PasswordSalt)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.BeltRank)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Academy)
            .HasMaxLength(200);

        builder.Property(e => e.Role)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.HasOne(e => e.Settings)
            .WithOne(e => e.User)
            .HasForeignKey<UserSettings>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
