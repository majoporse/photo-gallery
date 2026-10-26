using System.Net;
using System.Net.Http.Json;
using Gallery.Application.Gallery;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gallery.Api.Tests;

public sealed class GalleryEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-3")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task Invalid_paging_is_rejected_before_any_query(string query)
    {
        using var client = WithFakeQueries().CreateClient();

        var response = await client.GetAsync($"/api/v1/collections?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Paging_defaults_are_24_per_page()
    {
        using var client = WithFakeQueries().CreateClient();

        var response = await client.GetAsync("/api/v1/collections");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<CollectionSummary>>();
        Assert.Equal(1, body!.Page);
        Assert.Equal(24, body.PageSize);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task Unknown_slug_is_a_problem_details_404()
    {
        using var client = WithFakeQueries().CreateClient();

        var response = await client.GetAsync("/api/v1/collections/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private WebApplicationFactory<Program> WithFakeQueries() =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPublicGalleryQueries>();
                services.AddSingleton<IPublicGalleryQueries, EmptyGalleryQueries>();
            }));

    private sealed class EmptyGalleryQueries : IPublicGalleryQueries
    {
        public Task<PagedResult<CollectionSummary>> ListCollectionsAsync(
            int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<CollectionSummary>([], page, pageSize, 0));

        public Task<CollectionSummary?> GetCollectionAsync(string slug, CancellationToken cancellationToken) =>
            Task.FromResult<CollectionSummary?>(null);

        public Task<PagedResult<PhotoSummary>?> ListPhotosAsync(
            string slug, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult<PagedResult<PhotoSummary>?>(new PagedResult<PhotoSummary>([], page, pageSize, 0));

        public Task<PhotoDetail?> GetPhotoAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<PhotoDetail?>(null);

        public Task<HomeFeed> GetHomeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new HomeFeed([], []));

        public Task<IReadOnlyList<PhotoSummary>> GetIntroAsync(
            int count,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PhotoSummary>>([]);
    }
}
