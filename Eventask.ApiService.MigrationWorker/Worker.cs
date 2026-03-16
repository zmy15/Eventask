using System.Diagnostics;
using Eventask.ApiService.Repository;
using Microsoft.EntityFrameworkCore;

namespace Eventask.ApiService.MigrationWorker;

public class Worker(
    ILogger<Worker> logger,
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime
    ) : BackgroundService
{
    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource s_activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(
        CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity(
            "Migrating database", ActivityKind.Client);

        try
        {
            logger.LogInformation("Starting database migration...");

            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EventaskContext>();

            logger.LogInformation("Applying migrations...");
            await RunMigrationAsync(dbContext, cancellationToken);
            logger.LogInformation("Seeding initial data...");
            await SeedDataAsync(dbContext, cancellationToken);

            logger.LogInformation("Database migration completed.");
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            logger.LogError(ex, "An error occurred during database migration.");
            throw;
        }

        hostApplicationLifetime.StopApplication();
    }

    private static async Task RunMigrationAsync(
        EventaskContext dbContext, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            // Run migration in a transaction to avoid partial migration if it fails.
            await dbContext.Database.MigrateAsync(cancellationToken);
        });
    }

    private static async Task SeedDataAsync(
        EventaskContext dbContext, CancellationToken cancellationToken)
    {
        //SupportTicket firstTicket = new()
        //{
        //    Title = "Test Ticket",
        //    Description = "Default ticket, please ignore!",
        //    Completed = true
        //};

        //var strategy = dbContext.Database.CreateExecutionStrategy();
        //await strategy.ExecuteAsync(async ( ) =>
        //{
        //    // Seed the database
        //    await using var transaction = await dbContext.Database
        //        .BeginTransactionAsync(cancellationToken);

        //    await dbContext.Tickets.AddAsync(firstTicket, cancellationToken);
        //    await dbContext.SaveChangesAsync(cancellationToken);
        //    await transaction.CommitAsync(cancellationToken);
        //});
    }
}
