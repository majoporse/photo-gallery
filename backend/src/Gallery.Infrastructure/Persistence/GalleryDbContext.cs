using Gallery.Domain.Collections;
using Gallery.Domain.Photos;
using Microsoft.EntityFrameworkCore;

namespace Gallery.Infrastructure.Persistence;

public sealed class GalleryDbContext(DbContextOptions<GalleryDbContext> options) : DbContext(options)
{
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<Photo> Photos => Set<Photo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GalleryDbContext).Assembly);
}
