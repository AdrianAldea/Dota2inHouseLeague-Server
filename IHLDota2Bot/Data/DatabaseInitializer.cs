using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace IHLDota2Bot.Data;

/// <summary>
/// Brings the database schema up to date on startup using EF Core migrations.
/// Replaces the previous <c>EnsureCreated</c> approach, which could never evolve a
/// schema after the first run. Databases originally created by <c>EnsureCreated</c>
/// are transparently adopted into migration control (see <see cref="SchemaUpgradeAction"/>).
/// </summary>
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
            logger.LogInformation("Applying database migrations...");
            // Use the configured retry strategy so the very first connection rides
            // out a SQL Server container that's still finishing startup.
            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(() => MigrateAsync(context, cancellationToken));
            logger.LogInformation("Database ready.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize database schema");
            throw;
        }
    }

    private async Task MigrateAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        // Non-relational providers (the in-memory store used by integration tests)
        // don't support migrations; create the schema directly.
        if (!context.Database.IsRelational())
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
            return;
        }

        var creator = context.GetService<IRelationalDatabaseCreator>();
        var history = context.GetService<IHistoryRepository>();

        var databaseExists = await creator.ExistsAsync(cancellationToken);
        var historyExists = databaseExists && await history.ExistsAsync(cancellationToken);
        var hasTables = databaseExists && await creator.HasTablesAsync(cancellationToken);

        var action = SchemaUpgradePlanner.Decide(databaseExists, historyExists, hasTables);
        if (action == SchemaUpgradeAction.BaselineThenMigrate)
        {
            logger.LogWarning(
                "Existing schema found with no migration history (legacy EnsureCreated database). "
                + "Baselining the initial migration before applying any newer ones.");
            await BaselineInitialMigrationAsync(context, history, cancellationToken);
        }

        await context.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// Stamps the first migration as already applied without running its <c>Up()</c>,
    /// so a database whose schema predates migrations (created by <c>EnsureCreated</c>)
    /// is adopted into migration control instead of EF trying to recreate its tables.
    /// Only the initial migration is recorded; any later migrations still run normally.
    /// </summary>
    private static async Task BaselineInitialMigrationAsync(
        AppDbContext context, IHistoryRepository history, CancellationToken cancellationToken)
    {
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var initialMigrationId = migrationsAssembly.Migrations.Keys.First();

        var script = new StringBuilder();
        script.Append(history.GetCreateScript());
        script.Append(history.GetInsertScript(new HistoryRow(initialMigrationId, ProductInfo.GetVersion())));

        await context.Database.ExecuteSqlRawAsync(script.ToString(), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
