using Gallery.Application.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Gallery.Infrastructure.Storage;

public static class StorageRegistration
{
    public static IServiceCollection AddObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ObjectStorageOptions>().BindConfiguration(ObjectStorageOptions.SectionName);

        services.AddSingleton<IObjectStore>(provider =>
            new S3ObjectStore(provider.GetRequiredService<IOptions<ObjectStorageOptions>>()));
        services.AddSingleton<IImageProcessor, MagickImageProcessor>();
        services.AddSingleton<IImageFormatDetector, FileSignaturesImageFormatDetector>();
        services.AddSingleton<UploadPipeline>();

        return services;
    }
}
