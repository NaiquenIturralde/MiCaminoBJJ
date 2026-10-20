using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class UserProfileMediaConfiguration : IEntityTypeConfiguration<UserProfileMedia>
{
    public void Configure(EntityTypeBuilder<UserProfileMedia> builder)
    {
        builder.ToTable("UserProfileMedia");

        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.User)
            .WithMany(e => e.ProfileMedia)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.MediaAsset)
            .WithMany(e => e.ProfileMedia)
            .HasForeignKey(e => e.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        // At most one active profile picture per user
        builder.HasIndex(e => new { e.UserId, e.IsProfilePicture })
            .HasFilter("[IsProfilePicture] = 1")
            .IsUnique();
    }
}
