var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithDataVolume("eventask-dev-postgres-data");

var postgresdb = postgres.AddDatabase("postgres-db");

var migrations = builder.AddProject<Projects.Eventask_ApiService_MigrationWorker>("eventask-apiservice-migrationworker")
    .WithReference(postgresdb)
    .WaitFor(postgresdb);

var apiService = builder.AddProject<Projects.Eventask_ApiService>("eventask-apiservice")
    .WithUrl("/scalar")
    .WithHttpHealthCheck("/health")
    .WithReference(postgresdb)
    .WithReference(migrations)
    .WaitForCompletion(migrations);

builder.AddProject<Projects.Eventask_Desktop>("eventask-desktop")
    .WithEnvironment("AppOptions__ApiBackendUrl", apiService.GetEndpoint("https"))
    .WithEnvironment("DOTNET_ENVIRONMENT", "Development");

builder.Build().Run();
