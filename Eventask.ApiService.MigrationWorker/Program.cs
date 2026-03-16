using Eventask.ApiService.MigrationWorker;
using Eventask.ApiService.Repository;
using Eventask.ApiService.Utilities;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<EventaskContext>("postgres-db");
builder.Services.AddHostedService<Worker>();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

var host = builder.Build();
host.Run();
