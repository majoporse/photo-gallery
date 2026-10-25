using System.Text;
using System.Threading.RateLimiting;
using Gallery.Api.Admin;
using Gallery.Api.Configuration;
using Gallery.Api.Endpoints;
using Gallery.Application.Admin;
using Gallery.Application.Gallery;
using Gallery.Infrastructure.Persistence;
using Gallery.Infrastructure.Persistence.Commands;
using Gallery.Infrastructure.Persistence.Queries;
using Gallery.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// Configured credentials are a hash, never a password; this is how you make one.
if (args is ["hash-password", var passwordToHash])
{
    Console.WriteLine(PasswordHasher.Hash(passwordToHash));
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1");

builder.Services.AddObjectStorage(builder.Configuration);

// Empty rather than null: addresses are configured in every real environment,
// and a null here would fail at registration instead of at first query.
builder.Services.AddDbContext<GalleryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Gallery") ?? string.Empty));
builder.Services.AddScoped<IPublicGalleryQueries, PublicGalleryQueries>();
builder.Services.AddScoped<IAdminGallery, AdminGallery>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOptions<AdminOptions>().BindConfiguration(AdminOptions.SectionName);
builder.Services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName);
builder.Services.AddOptions<CorsOptions>().BindConfiguration(CorsOptions.SectionName);

var signingKey = builder.Configuration[ConfigurationKeys.Auth.SigningKey];
if (string.IsNullOrWhiteSpace(signingKey))
{
    // Readiness already fails without it; this placeholder only keeps the DI
    // container constructible for the build-time OpenAPI generator, which never
    // validates a token.
    signingKey = "openapi-generation-placeholder-signing-key";
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration[ConfigurationKeys.Auth.Issuer] ?? AuthOptions.DefaultIssuer,
            ValidateAudience = true,
            ValidAudience = builder.Configuration[ConfigurationKeys.Auth.Audience] ?? AuthOptions.DefaultAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        // The token lives in a cookie; the browser sends it; the handler reads it.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue(AdminCookies.Name, out var token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<AdminSessionService>();

var allowedOrigin = builder.Configuration[ConfigurationKeys.Cors.AllowedOrigin];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (!string.IsNullOrWhiteSpace(allowedOrigin))
    {
        policy.WithOrigins(allowedOrigin).AllowCredentials().AllowAnyHeader().AllowAnyMethod();
    }
}));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

var app = builder.Build();

// One-shot migration entry point, used by Compose and the chart's pre-upgrade
// Job: `dotnet Gallery.Api.dll migrate`. Never runs implicitly, so two replicas
// cannot race each other on a rollout.
if (args is ["migrate"])
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    await migrationScope.ServiceProvider.GetRequiredService<GalleryDbContext>().Database.MigrateAsync();
    return;
}

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

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api/v1")
    .MapHealthEndpoints()
    .MapGalleryEndpoints();

api.MapGroup("/admin")
    .AddEndpointFilter<AdminRequestGuard>()
    .RequireAuthorization()
    .MapAdminSessionEndpoints()
    .MapAdminGalleryEndpoints();

app.Run();

public partial class Program;
