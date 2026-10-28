using Gallery.Domain.Photos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gallery.Persistence.Configurations;

public sealed class PhotoConfiguration : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.ToTable("Photos", table =>
        {
            table.HasCheckConstraint("CK_Photos_Position", "\"Position\" >= 0");
            table.HasCheckConstraint("CK_Photos_FeaturedRank", "\"FeaturedRank\" IS NULL OR \"FeaturedRank\" >= 0");
            table.HasCheckConstraint("CK_Photos_Dimensions", "\"Width\" > 0 AND \"Height\" > 0");
        });

        builder.HasKey(photo => photo.Id);

        builder.Property(photo => photo.Id).HasMaxLength(26);
        builder.Property(photo => photo.CollectionId).HasMaxLength(26).IsRequired();
        builder.Property(photo => photo.Title).HasMaxLength(300);
        builder.Property(photo => photo.Caption).HasMaxLength(4000);
        builder.Property(photo => photo.AltText).HasMaxLength(500);
        builder.Property(photo => photo.OriginalKey).HasMaxLength(400).IsRequired();
        builder.Property(photo => photo.OriginalSha256).HasMaxLength(64).IsRequired();
        builder.Property(photo => photo.DominantColor).HasMaxLength(9);
        builder.Property(photo => photo.ExifJson).HasColumnType("jsonb").IsRequired();

        builder.HasOne<Gallery.Domain.Collections.Collection>()
            .WithMany()
            .HasForeignKey(photo => photo.CollectionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique among live photos only: a soft-deleted photo must not keep
        // occupying a position, or closing the gap collides with it.
        builder.HasIndex(photo => new { photo.CollectionId, photo.Position })
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");
        builder.HasIndex(photo => photo.FeaturedRank)
            .HasFilter("\"FeaturedRank\" IS NOT NULL");

        builder.HasQueryFilter(photo => photo.DeletedAt == null);
    }
}
