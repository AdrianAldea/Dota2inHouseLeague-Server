using Microsoft.EntityFrameworkCore;

namespace IHLDota2Bot.Data;

public class DatabaseInitializer : IHostedService
{
    private readonly IDbContextFactory<AppDbContext> contextFactory;
    private readonly ILogger<DatabaseInitializer> logger;

    public DatabaseInitializer(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<DatabaseInitializer> logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(logger);

        this.contextFactory = contextFactory;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        try
        {
            logger.LogInformation("Ensuring database schema exists...");
            // Use the configured retry strategy so the very first connection rides
            // out a SQL Server container that's still finishing startup.
            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(() => context.Database.EnsureCreatedAsync(cancellationToken));
            logger.LogInformation("Database ready.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize database schema");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
