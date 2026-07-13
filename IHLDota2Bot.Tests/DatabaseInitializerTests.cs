using IHLDota2Bot.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace IHLDota2Bot.Tests;

/// <summary>
/// Exercises the real migration startup path against a relational (SQLite)
/// database so the legacy <c>EnsureCreated</c> → migrations transition is
/// verified end to end, not just at the decision-table level.
/// </summary>
public class DatabaseInitializerTests : IDisposable
{
    // A single open in-memory connection so the schema survives across the
    // multiple DbContext instances the factory hands out.
    private readonly SqliteConnection connection;

    public DatabaseInitializerTests()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
    }

    public void Dispose() => connection.Dispose();

    private SharedConnectionContextFactory CreateFactory() => new(connection);

    private static async Task RunInitializerAsync(IDbContextFactory<AppDbContext> factory) =>
        await new DatabaseInitializer(factory, NullLogger<DatabaseInitializer>.Instance).StartAsync(
            CancellationToken.None
        );

    [Fact]
    public async Task FreshDatabase_AppliesMigrations_AndRecordsHistory()
    {
        var factory = CreateFactory();

        await RunInitializerAsync(factory);

        await using var context = factory.CreateDbContext();
        var applied = await context.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(applied);
        // Schema is usable: a round-trip insert/read succeeds.
        Assert.Equal(0, await context.Players.CountAsync());
    }

    [Fact]
    public async Task LegacyEnsureCreatedDatabase_IsBaselined_WithoutError()
    {
        // Simulate the old world: schema created by EnsureCreated, no migration history.
        await using (var seed = CreateFactory().CreateDbContext())
        {
            await seed.Database.EnsureCreatedAsync();
        }

        var factory = CreateFactory();
        await using (var before = factory.CreateDbContext())
        {
            Assert.Empty(await before.Database.GetAppliedMigrationsAsync());
        }

        // Should adopt the existing schema into migration control rather than
        // trying to recreate the already-present tables (which would throw).
        await RunInitializerAsync(factory);

        await using var after = factory.CreateDbContext();
        var applied = await after.Database.GetAppliedMigrationsAsync();
        var expected = after.GetService<IMigrationsAssembly>().Migrations.Keys.First();
        Assert.Contains(expected, applied);
    }

    [Fact]
    public async Task RunningTwice_IsIdempotent()
    {
        var factory = CreateFactory();

        await RunInitializerAsync(factory);
        await RunInitializerAsync(factory);

        await using var context = factory.CreateDbContext();
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
    }

    /// <summary>
    /// Minimal <see cref="IDbContextFactory{TContext}"/> that points every context
    /// at one already-open SQLite connection — the in-memory database only lives as
    /// long as that connection is open.
    /// </summary>
    private sealed class SharedConnectionContextFactory(SqliteConnection connection)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                // The migrations are scaffolded for SQL Server; applying them through
                // SQLite makes the cross-provider model fingerprint differ, which EF
                // flags as "pending changes". Production runs on SQL Server (the
                // scaffold provider), so this only needs suppressing under test.
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
                .Options;
            return new AppDbContext(options);
        }
    }
}
