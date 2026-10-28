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

        // The gallery only signs, checks and deletes objects. Rendering lives in
        // the worker, which has its own Magick and its own copy of this adapter.
        services.AddSingleton<IObjectStore, S3ObjectStore>();

        return services;
    }
}
