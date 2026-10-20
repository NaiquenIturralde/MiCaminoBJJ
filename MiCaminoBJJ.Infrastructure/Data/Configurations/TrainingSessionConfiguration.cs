using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class TrainingSessionConfiguration : IEntityTypeConfiguration<TrainingSession>
{
    public void Configure(EntityTypeBuilder<TrainingSession> builder)
    {
        builder.ToTable("TrainingSessions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TrainingType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.Feeling)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        builder.HasOne(e => e.User)
            .WithMany(e => e.TrainingSessions)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.Date);
    }
}
