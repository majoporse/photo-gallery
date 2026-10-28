using Gallery.Domain.Uploads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gallery.Infrastructure.Persistence.Configurations;

public sealed class UploadConfiguration : IEntityTypeConfiguration<Upload>
{
    public void Configure(EntityTypeBuilder<Upload> builder)
    {
        builder.ToTable("Uploads", table =>
        {
            table.HasCheckConstraint("CK_Uploads_Position", "\"Position\" >= 0");
            table.HasCheckConstraint("CK_Uploads_Bytes", "\"DeclaredBytes\" > 0");
        });

        builder.HasKey(upload => upload.Id);

        builder.Property(upload => upload.Id).HasMaxLength(26);
        builder.Property(upload => upload.CollectionId).HasMaxLength(26).IsRequired();
        builder.Property(upload => upload.PhotoId).HasMaxLength(26).IsRequired();
        builder.Property(upload => upload.FileName).HasMaxLength(200).IsRequired();
        builder.Property(upload => upload.ObjectKey).HasMaxLength(400).IsRequired();

        // Stored as text: a queue table is read by humans and by SQL, and an
        // ordinal is neither stable across edits nor self-describing.
        builder.Property(upload => upload.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(upload => upload.Rejection).HasConversion<string>().HasMaxLength(40);
        builder.Property(upload => upload.Failure).HasMaxLength(1000);

        builder.HasOne<Gallery.Domain.Collections.Collection>()
            .WithMany()
            .HasForeignKey(upload => upload.CollectionId)
            .OnDelete(DeleteBehavior.Restrict);

        // The sweeper scans by state and age; the admin reads by collection.
        builder.HasIndex(upload => new { upload.Status, upload.UpdatedAt });
        builder.HasIndex(upload => upload.CollectionId);
    }
}
