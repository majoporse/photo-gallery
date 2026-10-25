using Gallery.Api.Configuration;
using Gallery.Api.Endpoints;
using Gallery.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1");

builder.Services.AddObjectStorage(builder.Configuration);

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

app.MapGroup("/api/v1").MapHealthEndpoints();

app.Run();

public partial class Program;
