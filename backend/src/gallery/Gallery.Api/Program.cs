using System.Text;
using System.Threading.RateLimiting;
using Gallery.Api.Admin;
using Gallery.Api.Configuration;
using Gallery.Api.Uploads;
using Gallery.Application.Admin;
using Gallery.Application.Gallery;
using Gallery.Application.Uploads;
using Gallery.Contracts.Uploads;
using Gallery.Infrastructure;
using Gallery.Infrastructure.Seeding;
using Gallery.Infrastructure.Storage;
using Gallery.Infrastructure.Uploads;
using Gallery.Persistence;
using Gallery.Persistence.Commands;
using Gallery.Persistence.Queries;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Wolverine;
using Wolverine.Postgresql;

// Configured credentials are a hash, never a password; this is how you make one.
if (args is ["hash-password", var passwordToHash])
{
    Console.WriteLine(PasswordHasher.Hash(passwordToHash));
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1");

// Model validation keys an error by the CLR property name; the public contract
// has always used the camelCase query names, so translate rather than rename
// the properties and leave the JSON root looking like a .NET type.
builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problems = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var modelState = new ModelStateDictionary();

        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0)
            {
                continue;
            }

            var field = key.Length > 0 && char.IsUpper(key[0])
                ? char.ToLowerInvariant(key[0]) + key[1..]
                : key;

            foreach (var error in entry.Errors)
            {
                modelState.AddModelError(
                    field,
                    string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The value is not valid." : error.ErrorMessage);
            }
        }

        var problem = problems.CreateValidationProblemDetails(context.HttpContext, modelState);

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" },
        };
    };
});

// TLS terminates at the ingress. The docs page needs the public scheme to build
// an absolute OpenAPI document URL, so trust the ingress's forwarded headers.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedHost |
        ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddObjectStorage(builder.Configuration);
builder.Services.AddDemoSeeding(builder.Configuration);

// Empty rather than null: addresses are configured in every real environment,
// and a null here would fail at registration instead of at first query.
// A factory rather than a scoped context because Wolverine generates the call
// site of the reply handler and will not resolve the scoped registration; the
// scoped one below keeps the rest of the API injecting a context as before.
builder.Services.AddDbContextFactory<GalleryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Gallery") ?? string.Empty));
builder.Services.AddScoped(provider =>
    provider.GetRequiredService<IDbContextFactory<GalleryDbContext>>().CreateDbContext());
builder.Services.AddScoped<IPublicGalleryQueries, PublicGalleryQueries>();
builder.Services.AddScoped<IAdminGallery, AdminGallery>();
builder.Services.AddScoped<IUploadService, UploadCommands>();
builder.Services.AddSingleton<UploadStreamHub>();
builder.Services.AddSingleton<IUploadStream>(provider => provider.GetRequiredService<UploadStreamHub>());
builder.Services.AddHostedService<UploadNotificationListener>();
builder.Services.AddHostedService<UploadSweeper>();
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
builder.Services.AddScoped<AdminRequestGuard>();

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
    var permitLimit = builder.Configuration.GetValue(
        ConfigurationKeys.Auth.LoginPermitLimit,
        new AuthOptions().LoginPermitLimit);

    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

// The queue is only wired when there is a database to put it in. The build-time
// OpenAPI generator and the API tests have no connection string, and neither
// should start a message bus to generate a document or answer a route.
var galleryConnection = builder.Configuration.GetConnectionString("Gallery");

if (!string.IsNullOrWhiteSpace(galleryConnection))
{
    builder.Services.AddScoped<IUploadQueue, WolverineUploadQueue>();

    builder.UseWolverine(options =>
    {
        options.UseUploadQueues(galleryConnection);
        // The worker answers here. Nothing in this process consumes the commands
        // it publishes, and nothing there consumes its own answers.
        options.ListenToPostgresqlQueue(UploadTransport.Results);
    });
}
else
{
    builder.Services.AddScoped<IUploadQueue, UnconfiguredUploadQueue>();
}

var app = builder.Build();

// One-shot migration entry point, used by Compose and the chart's pre-upgrade
// Job: `dotnet Gallery.Api.dll migrate`. Never runs implicitly, so two replicas
// cannot race each other on a rollout.
if (args is ["migrate"] or ["seed"])
{
    await using var scope = app.Services.CreateAsyncScope();
    var services = scope.ServiceProvider;

    if (args is ["migrate"])
    {
        await services.GetRequiredService<GalleryDbContext>().Database.MigrateAsync();
    }

    // `migrate` also seeds, because a fresh development environment should come
    // up with something to look at. Off unless Seed:Enabled is set, so a
    // production migration can never invent content.
    if (services.GetRequiredService<IOptions<DemoSeedOptions>>().Value.Enabled)
    {
        // Seeding stages work and publishes nothing: a one-shot command has no
        // running host to flush a message, so the staged rows are left for the
        // sweeper, which is the recovery path a real lost publish uses anyway.
        await services.GetRequiredService<DemoSeeder>().SeedAsync(CancellationToken.None);
    }
    else
    {
        app.Logger.LogInformation("Demo seeding is disabled (Seed:Enabled).");
    }

    return;
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

// Scalar redirects "/docs" to "/docs/" because its asset URLs are relative.
// Rewriting the path after routing keeps the public URL working with a 200;
// the <base> set below then resolves the assets under "/docs/".
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/docs")
    {
        context.Request.Path = "/docs/";
    }

    await next();
});

// The contract is already committed and public, so the document and its
// rendered page are served in every environment.
app.MapOpenApi();
app.MapScalarApiReference("/docs", (options, context) =>
{
    options.Title = "Photo Gallery API";
    options.HeadContent = """<base href="/docs/" />""";
    // Scalar resolves a relative source against the docs page, so the document
    // URL must be absolute. The forwarded headers make the scheme https when
    // the browser reached the ingress over https.
    options.OpenApiRoutePattern =
        $"{context.Request.Scheme}://{context.Request.Host}/openapi/{{documentName}}.json";
});

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

app.MapControllers();

app.Run();

public partial class Program;
