using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class UserTournamentConfiguration : IEntityTypeConfiguration<UserTournament>
{
    public void Configure(EntityTypeBuilder<UserTournament> builder)
    {
        builder.ToTable("UserTournaments");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Placement)
            .HasMaxLength(100);

        builder.Property(e => e.Division)
            .HasMaxLength(200);

        builder.Property(e => e.Notes)
            .HasMaxLength(1000);

        builder.HasOne(e => e.User)
            .WithMany(e => e.Tournaments)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.UserId);
    }
}
