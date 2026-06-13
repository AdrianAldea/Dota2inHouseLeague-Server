using IHLDota2Bot.Data;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace IHLDota2Bot.Tests.TestSupport;

/// <summary>
/// Builds a Moq <see cref="IDbContextFactory{TContext}"/> backed by a real EF Core
/// in-memory database. The repositories under test open a fresh context per call
/// via the factory, so we mock the factory but hand back real contexts that all
/// share one in-memory store. That keeps the tests exercising the actual LINQ /
/// ordering / projection logic in the repositories rather than a hand-stubbed
/// query result — i.e. "real cases", not just behaviour-verification of a mock.
/// </summary>
internal static class InMemoryDb
{
    /// <summary>
    /// Creates a factory bound to its own isolated database (unique name per call),
    /// so tests never bleed state into one another.
    /// </summary>
    public static Mock<IDbContextFactory<AppDbContext>> CreateFactory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ihl-tests-{Guid.NewGuid()}")
            .Options;

        var factory = new Mock<IDbContextFactory<AppDbContext>>();

        // A new context each call (mirrors the production `await using var context`
        // lifetime) — all pointed at the same store via the shared options.
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new AppDbContext(options));
        factory
            .Setup(f => f.CreateDbContext())
            .Returns(() => new AppDbContext(options));

        return factory;
    }

    /// <summary>Opens a throwaway context against the same store for arrange/assert.</summary>
    public static async Task<AppDbContext> NewContextAsync(this Mock<IDbContextFactory<AppDbContext>> factory)
        => await factory.Object.CreateDbContextAsync();
}
