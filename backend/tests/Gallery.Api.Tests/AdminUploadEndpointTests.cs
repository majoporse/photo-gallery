using System.Net;
using System.Net.Http.Json;
using Gallery.Api.Admin;
using Gallery.Api.Configuration;
using Gallery.Application.Admin;
using Gallery.Application.Uploads;
using Gallery.Contracts.Uploads;
using Gallery.Domain.Uploads;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gallery.Api.Tests;

public sealed class AdminUploadEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Username = "admin";
    private const string Password = "correct horse battery staple";
    private const string CollectionId = "01J0000000000000000000000";

    // Lower iteration count only to keep the suite fast; the format is identical.
    private static readonly string PasswordHash = PasswordHasher.Hash(Password, iterations: 10_000);

    [Fact]
    public async Task Beginning_an_upload_returns_a_signed_destination_and_a_position()
    {
        var uploads = new RecordingUploads();
        using var client = await SignedInClient(uploads);

        using var response = await Begin(client, fileName: "one.jpg", bytes: 2048);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var ticket = await response.Content.ReadFromJsonAsync<PresignedUpload>();
        Assert.NotNull(ticket);
        Assert.Equal("01J0000000000000000000001/one.jpg", ticket.Key);
        Assert.Contains("/originals/01J0000000000000000000001/one.jpg?", ticket.Url);
        Assert.Equal(3, ticket.Position);

        Assert.Equal((CollectionId, "one.jpg", 2048L), uploads.Begin);
    }

    [Fact]
    public async Task A_file_that_is_too_large_is_a_validation_problem()
    {
        var uploads = new RecordingUploads { BeginError = AdminError.InvalidUpload };
        using var client = await SignedInClient(uploads);

        using var response = await Begin(client, fileName: "huge.jpg", bytes: 60 * 1024 * 1024);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Beginning_an_upload_in_a_missing_collection_is_a_404()
    {
        var uploads = new RecordingUploads { BeginError = AdminError.NotFound };
        using var client = await SignedInClient(uploads);

        using var response = await Begin(client, fileName: "one.jpg", bytes: 10);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Completing_an_upload_answers_accepted_because_processing_is_asynchronous()
    {
        var uploads = new RecordingUploads
        {
            CompleteView = View("01J0000000000000000000001", UploadStatus.Queued),
        };
        using var client = await SignedInClient(uploads);

        using var response = await Complete(client, "01J0000000000000000000001");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var view = await response.Content.ReadFromJsonAsync<UploadView>();
        Assert.Equal("Queued", view?.Status);
    }

    [Fact]
    public async Task Completing_an_upload_whose_bytes_never_arrived_is_a_conflict()
    {
        var uploads = new RecordingUploads { CompleteError = AdminError.NotUploaded };
        using var client = await SignedInClient(uploads);

        using var response = await Complete(client, "01J0000000000000000000001");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_mutation_without_the_csrf_header_is_refused()
    {
        var uploads = new RecordingUploads();
        using var client = await SignedInClient(uploads);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/admin/collections/{CollectionId}/uploads")
        {
            Content = JsonContent.Create(new BeginUpload("one.jpg", 10)),
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(uploads.Begin);
    }

    private async Task<HttpClient> SignedInClient(IUploadService uploads)
    {
        var client = factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(ConfigurationKeys.Admin.Username, Username);
                builder.UseSetting(ConfigurationKeys.Admin.PasswordHash, PasswordHash);
                builder.UseSetting(ConfigurationKeys.Auth.SigningKey, new string('k', 48));
                builder.UseSetting(ConfigurationKeys.Auth.CookieSecure, "false");
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IUploadService>();
                    services.AddSingleton(uploads);
                });
            })
            .CreateClient();

        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/session")
        {
            Content = JsonContent.Create(new { username = Username, password = Password }),
        };
        login.Headers.Add(AdminCookies.RequiredHeader, AdminCookies.RequiredHeaderValue);

        var response = await client.SendAsync(login);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        return client;
    }

    private static async Task<HttpResponseMessage> Begin(
        HttpClient client,
        string fileName,
        long bytes)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/admin/collections/{CollectionId}/uploads")
        {
            Content = JsonContent.Create(new BeginUpload(fileName, bytes)),
        };
        request.Headers.Add(AdminCookies.RequiredHeader, AdminCookies.RequiredHeaderValue);

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> Complete(HttpClient client, string uploadId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/admin/uploads/{uploadId}/complete");
        request.Headers.Add(AdminCookies.RequiredHeader, AdminCookies.RequiredHeaderValue);

        return await client.SendAsync(request);
    }

    private static UploadView View(string id, UploadStatus status) =>
        new(
            id,
            CollectionId,
            "one.jpg",
            3,
            status.ToString(),
            null,
            null,
            null,
            DateTimeOffset.UnixEpoch);

    private sealed class RecordingUploads : IUploadService
    {
        public (string CollectionId, string FileName, long Bytes)? Begin { get; private set; }

        public AdminError? BeginError { get; init; }

        public AdminError? CompleteError { get; init; }

        public UploadView? CompleteView { get; init; }

        public Task<AdminResult<PresignedUpload>> BeginAsync(
            string collectionId,
            BeginUpload command,
            CancellationToken cancellationToken)
        {
            Begin = (collectionId, command.FileName, command.Bytes);

            if (BeginError is { } error)
            {
                return Task.FromResult(AdminResult<PresignedUpload>.Fail(error));
            }

            return Task.FromResult(AdminResult<PresignedUpload>.Success(
                new PresignedUpload(
                    "01J0000000000000000000001",
                    "01J0000000000000000000001/one.jpg",
                    "https://img.example.test/originals/01J0000000000000000000001/one.jpg?X-Amz-Signature=abc",
                    DateTimeOffset.UnixEpoch.AddMinutes(15),
                    3)));
        }

        public Task<AdminResult<UploadView>> CompleteAsync(
            string uploadId,
            CancellationToken cancellationToken) =>
            Task.FromResult(CompleteError is { } error
                ? AdminResult<UploadView>.Fail(error)
                : AdminResult<UploadView>.Success(CompleteView!));

        public Task<IReadOnlyList<UploadView>> ListActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UploadView>>([]);
    }
}
