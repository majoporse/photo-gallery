using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gallery.Api.Tests;

public sealed class DocsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/docs")]
    [InlineData("/openapi/v1.json")]
    public async Task Docs_are_served_outside_development(string path)
    {
        using var client = ProductionClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Docs_load_their_assets_from_the_docs_path()
    {
        using var client = ProductionClient();

        var html = await client.GetStringAsync("/docs");

        Assert.Contains("""<base href="/docs/" />""", html);
        Assert.Contains("src=\"scalar.aspnetcore.js\"", html);
    }

    [Fact]
    public async Task Document_url_uses_the_scheme_forwarded_by_the_ingress()
    {
        using var client = ProductionClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/docs");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "api.example.test");

        var html = await (await client.SendAsync(request)).Content.ReadAsStringAsync();

        Assert.Contains(
            "\"url\":\"https://api.example.test/openapi/v1.json\"",
            html);
    }

    private HttpClient ProductionClient() =>
        factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"))
            .CreateClient();
}
