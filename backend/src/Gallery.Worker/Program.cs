using Gallery.Application.Uploads;
using Gallery.Persistence;
using Gallery.Infrastructure.Storage;
using Gallery.Infrastructure.Uploads;
using Gallery.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Postgresql;

var builder = Host.CreateApplicationBuilder(args);

// A worker needs bytes, a database and a queue. It must never need the admin
// password or the CORS origin: rendering a JPEG is not an authenticated act.
var connectionString = builder.Configuration.GetConnectionString("Gallery")
    ?? throw new InvalidOperationException("ConnectionStrings__Gallery is required.");

builder.Services.AddDbContextFactory<GalleryDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddObjectStorage(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IUploadQueue, WolverineUploadQueue>();
builder.Services.AddHostedService<UploadSweeper>();

builder.UseWolverine(options =>
{
    options.UsePostgresqlPersistenceAndTransport(
            connectionString,
            "gallery",
            transportSchema: "wolverine_queues")
        .AutoProvision()
        // Without this the worker polls every five seconds; with it, a NOTIFY
        // wakes the receive loop as soon as the row is committed.
        .EnableQueueNotifications();

    options.PublishMessage<ProcessUpload>().ToPostgresqlQueue("uploads");
    options.ListenToPostgresqlQueue("uploads");
    // The handler lives in Infrastructure (it depends only on shared services
    // and the integration tests call it directly). This is what finds it.
    options.Discovery.IncludeAssembly(typeof(ProcessUploadHandler).Assembly);
    options.Policies.DisableConventionalLocalRouting();
});

var host = builder.Build();
await host.RunAsync();
