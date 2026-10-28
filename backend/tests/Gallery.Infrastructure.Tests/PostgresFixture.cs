using Gallery.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Gallery.Infrastructure.Tests;

/// <summary>
/// One Postgres container and one migrated schema for the whole test class.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var database = CreateContext();
        await database.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public GalleryDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<GalleryDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options);
}
