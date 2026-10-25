using System.Net;
using System.Net.Http.Json;
using Gallery.Api.Admin;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gallery.Api.Tests;

public sealed class AdminAuthTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Username = "admin";
    private const string Password = "correct horse battery staple";
    private const string AllowedOrigin = "https://gallery.test";

    // Lower iteration count only to keep the suite fast; the format is identical.
    private static readonly string PasswordHash = PasswordHasher.Hash(Password, iterations: 10_000);

    [Fact]
    public async Task Correct_credentials_set_an_http_only_cookie()
    {
        using var client = Configured().CreateClient();

        var response = await Login(client, Password);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("pg_session=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Wrong_password_is_a_401()
    {
        using var client = Configured().CreateClient();

        var response = await Login(client, "wrong password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task The_session_endpoint_requires_the_cookie()
    {
        using var client = Configured().CreateClient();

        var response = await client.GetAsync("/api/v1/admin/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_valid_cookie_identifies_the_admin()
    {
        using var client = Configured().CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await Login(client, Password)).StatusCode);

        var response = await client.GetAsync("/api/v1/admin/session");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SessionBody>();
        Assert.Equal(Username, body!.Username);
    }

    [Fact]
    public async Task A_mutation_without_the_custom_header_is_rejected()
    {
        using var client = Configured().CreateClient();

        var response = await Login(client, Password, withHeader: false);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_mutation_from_a_foreign_origin_is_rejected()
    {
        using var client = Configured().CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/session")
        {
            Content = JsonContent.Create(new { username = Username, password = Password }),
        };
        request.Headers.Add("X-Requested-With", "photo-gallery");
        request.Headers.Add("Origin", "https://evil.example");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Repeated_failed_logins_are_rate_limited()
    {
        using var client = Configured().CreateClient();

        HttpResponseMessage response = null!;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            response = await Login(client, "wrong password");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    private WebApplicationFactory<Program> Configured() =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Admin:Username", Username);
            builder.UseSetting("Admin:PasswordHash", PasswordHash);
            builder.UseSetting("Auth:SigningKey", new string('k', 48));
            builder.UseSetting("Auth:CookieSecure", "false");
            builder.UseSetting("Cors:AllowedOrigin", AllowedOrigin);
        });

    private static Task<HttpResponseMessage> Login(HttpClient client, string password, bool withHeader = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/session")
        {
            Content = JsonContent.Create(new { username = Username, password }),
        };

        if (withHeader)
        {
            request.Headers.Add("X-Requested-With", "photo-gallery");
        }

        request.Headers.Add("Origin", AllowedOrigin);
        return client.SendAsync(request);
    }

    private sealed record SessionBody(string Username);
}
