using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Gallery.Persistence;

/// <summary>
/// Lets `dotnet ef` build the model without starting the API. Migrations are
/// generated offline; only `database update` needs a reachable server.
/// </summary>
public sealed class GalleryDbContextFactory : IDesignTimeDbContextFactory<GalleryDbContext>
{
    private const string FallbackConnection =
        "Host=localhost;Port=5432;Database=gallery;Username=gallery;Password=gallery";

    public GalleryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("GALLERY_CONNECTION") ?? FallbackConnection;

        return new GalleryDbContext(
            new DbContextOptionsBuilder<GalleryDbContext>()
                .UseNpgsql(connectionString)
                .Options);
    }
}
