using Gallery.Infrastructure.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gallery.Infrastructure;

public static class SeedingRegistration
{
    public static IServiceCollection AddDemoSeeding(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DemoSeedOptions>().BindConfiguration(DemoSeedOptions.SectionName);

        // A single long-lived client is right for a one-shot seeder; the factory
        // machinery would buy nothing here.
        services.AddSingleton<IDemoPhotoSource>(_ =>
            new PicsumPhotoSource(new HttpClient { Timeout = TimeSpan.FromSeconds(45) }));

        services.AddScoped<DemoSeeder>();

        return services;
    }
}
