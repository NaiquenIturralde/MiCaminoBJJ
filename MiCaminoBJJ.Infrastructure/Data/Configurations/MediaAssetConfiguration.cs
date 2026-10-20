using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MiCaminoBJJ.Core.Entities;

namespace MiCaminoBJJ.Infrastructure.Data.Configurations;

public class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("MediaAssets");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.MediaType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.SourceType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.LocalPath)
            .HasMaxLength(1000);

        builder.Property(e => e.RemoteUrl)
            .HasMaxLength(2000);

        builder.Property(e => e.ExternalUrl)
            .HasMaxLength(2000);

        builder.Property(e => e.ExternalProvider)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(e => e.ExternalVideoId)
            .HasMaxLength(200);

        builder.Property(e => e.OriginalFileName)
            .HasMaxLength(500);

        builder.Property(e => e.MimeType)
            .HasMaxLength(100);

        builder.Property(e => e.Title)
            .HasMaxLength(300);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.ThumbnailPath)
            .HasMaxLength(1000);

        builder.Property(e => e.SyncStatus)
            .IsRequired()
            .HasConversion<int>();

        builder.HasOne(e => e.UploadedBy)
            .WithMany(e => e.UploadedMedia)
            .HasForeignKey(e => e.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.IsDeleted);
        builder.HasIndex(e => e.UploadedByUserId);
    }
}
