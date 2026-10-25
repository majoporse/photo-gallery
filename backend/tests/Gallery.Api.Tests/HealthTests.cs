using System.Net;
using System.Net.Http.Json;
using Gallery.Api.Configuration;
using Gallery.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gallery.Api.Tests;

public sealed class HealthTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_answers_even_without_configuration()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthBody>();
        Assert.Equal("ok", body!.Status);
        Assert.Empty(body.MissingConfiguration);
    }

    [Fact]
    public async Task Readiness_names_the_missing_settings()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthBody>();
        Assert.Contains(ConfigurationKeys.Storage.Endpoint, body!.MissingConfiguration);
        Assert.Contains(ConfigurationKeys.Auth.SigningKey, body.MissingConfiguration);
    }

    [Fact]
    public async Task Readiness_passes_once_configuration_is_complete()
    {
        using var configured = factory.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in CompleteConfiguration())
            {
                builder.UseSetting(key, value);
            }
        });
        using var client = configured.CreateClient();

        var response = await client.GetAsync("/api/v1/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Dictionary<string, string> CompleteConfiguration() => new()
    {
        [ConfigurationKeys.Storage.Endpoint] = "http://seaweed-s3:8333",
        [ConfigurationKeys.Storage.PublicBucket] = ObjectStorageOptions.DefaultPublicBucket,
        [ConfigurationKeys.Storage.PrivateBucket] = ObjectStorageOptions.DefaultPrivateBucket,
        [ConfigurationKeys.Storage.AccessKey] = "test",
        [ConfigurationKeys.Storage.SecretKey] = "test",
        [ConfigurationKeys.Admin.Username] = "admin",
        [ConfigurationKeys.Admin.PasswordHash] = "test",
        [ConfigurationKeys.Auth.SigningKey] = "test-signing-key",
        [ConfigurationKeys.Cors.AllowedOrigin] = "http://localhost:5173",
    };

    private sealed record HealthBody(string Status, IReadOnlyList<string> MissingConfiguration);
}
