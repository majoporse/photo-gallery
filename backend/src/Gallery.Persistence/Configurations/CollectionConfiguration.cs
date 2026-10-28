using Gallery.Domain.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gallery.Persistence.Configurations;

public sealed class CollectionConfiguration : IEntityTypeConfiguration<Collection>
{
    public void Configure(EntityTypeBuilder<Collection> builder)
    {
        builder.ToTable("Collections", table =>
            table.HasCheckConstraint("CK_Collections_Position", "\"Position\" >= 0"));

        builder.HasKey(collection => collection.Id);

        builder.Property(collection => collection.Id).HasMaxLength(26);
        builder.Property(collection => collection.Slug).HasMaxLength(120).IsRequired();
        builder.Property(collection => collection.Name).HasMaxLength(200).IsRequired();
        builder.Property(collection => collection.Description).HasMaxLength(2000);

        builder.Property(collection => collection.NextPhotoPosition).HasDefaultValue(0);

        // Unique among live rows only: a deleted collection frees its slug.
        builder.HasIndex(collection => collection.Slug)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasIndex(collection => collection.Position);

        builder.HasQueryFilter(collection => collection.DeletedAt == null);
    }
}
