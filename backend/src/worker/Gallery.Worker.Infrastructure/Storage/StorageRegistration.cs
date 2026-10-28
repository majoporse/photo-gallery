using Gallery.Worker.Application.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gallery.Worker.Infrastructure.Storage;

public static class StorageRegistration
{
    public static IServiceCollection AddObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ObjectStorageOptions>().BindConfiguration(ObjectStorageOptions.SectionName);

        services.AddSingleton<IObjectStore, S3ObjectStore>();
        services.AddSingleton<IImageProcessor, MagickImageProcessor>();
        services.AddSingleton<IImageFormatDetector, FileSignaturesImageFormatDetector>();

        return services;
    }
}
