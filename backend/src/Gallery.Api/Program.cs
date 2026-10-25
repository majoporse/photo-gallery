using Gallery.Api.Configuration;
using Gallery.Api.Endpoints;
using Gallery.Application.Gallery;
using Gallery.Infrastructure.Persistence;
using Gallery.Infrastructure.Persistence.Queries;
using Gallery.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1");

builder.Services.AddObjectStorage(builder.Configuration);

// Empty rather than null: addresses are configured in every real environment,
// and a null here would fail at registration instead of at first query.
builder.Services.AddDbContext<GalleryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Gallery") ?? string.Empty));
builder.Services.AddScoped<IPublicGalleryQueries, PublicGalleryQueries>();

builder.Services.AddOptions<AdminOptions>().BindConfiguration(AdminOptions.SectionName);
builder.Services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName);
builder.Services.AddOptions<CorsOptions>().BindConfiguration(CorsOptions.SectionName);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// The OpenAPI document is produced by running this entry point at build time,
// where deployment configuration does not exist. Logging a missing-secret error
// there would make every build look broken, so the warning is skipped for it.
var isDocumentGeneration = args.Any(arg => arg.StartsWith("--applicationName", StringComparison.Ordinal));
if (!isDocumentGeneration)
{
    var missing = RequiredSettings.Missing(app.Configuration);
    if (missing.Count > 0)
    {
        app.Logger.LogError(
            "Missing required configuration: {MissingKeys}. The readiness probe will fail until these are set.",
            string.Join(", ", missing));
    }
}

app.MapGroup("/api/v1").MapHealthEndpoints().MapGalleryEndpoints();

app.Run();

public partial class Program;
