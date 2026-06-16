using IHLDota2Bot.Discord.Commands;
using IHLDota2Bot.Discord.Configuration;
using IHLDota2Bot.Discord.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace IHLDota2Bot.Discord.Tests;

/// <summary>
/// Per-user sliding-window throttle. Drives a <see cref="TestTimeProvider"/> so the
/// window expiry is deterministic rather than wall-clock dependent.
/// </summary>
public class CommandRateLimiterTests
{
    private static CommandRateLimiter Create(CommandRateLimitOptions options, TestTimeProvider clock) =>
        new(Options.Create(options), clock);

    [Fact]
    public void AllowsUpToBudgetWithinWindow()
    {
        var clock = new TestTimeProvider();
        var limiter = Create(new CommandRateLimitOptions { MaxCommands = 3, Window = TimeSpan.FromSeconds(5) }, clock);

        Assert.True(limiter.TryAcquire(1, out _));
        Assert.True(limiter.TryAcquire(1, out _));
        Assert.True(limiter.TryAcquire(1, out _));
    }

    [Fact]
    public void DeniesOnceBudgetExceeded_AndReportsRetryAfter()
    {
        var clock = new TestTimeProvider();
        var limiter = Create(new CommandRateLimitOptions { MaxCommands = 2, Window = TimeSpan.FromSeconds(5) }, clock);

        Assert.True(limiter.TryAcquire(1, out _));
        Assert.True(limiter.TryAcquire(1, out _));

        Assert.False(limiter.TryAcquire(1, out var retryAfter));
        Assert.True(retryAfter > TimeSpan.Zero);
        Assert.True(retryAfter <= TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AllowsAgainAfterWindowElapses()
    {
        var clock = new TestTimeProvider();
        var limiter = Create(new CommandRateLimitOptions { MaxCommands = 1, Window = TimeSpan.FromSeconds(5) }, clock);

        Assert.True(limiter.TryAcquire(1, out _));
        Assert.False(limiter.TryAcquire(1, out _));

        clock.Advance(TimeSpan.FromSeconds(5));

        Assert.True(limiter.TryAcquire(1, out _));
    }

    [Fact]
    public void PartialWindowElapse_StillDeniesUntilOldestAgesOut()
    {
        var clock = new TestTimeProvider();
        var limiter = Create(new CommandRateLimitOptions { MaxCommands = 1, Window = TimeSpan.FromSeconds(10) }, clock);

        Assert.True(limiter.TryAcquire(1, out _));

        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.False(limiter.TryAcquire(1, out var retryAfter));
        // ~6s left on the original hit's 10s window.
        Assert.InRange(retryAfter.TotalSeconds, 5.9, 6.1);
    }

    [Fact]
    public void UsersAreThrottledIndependently()
    {
        var clock = new TestTimeProvider();
        var limiter = Create(new CommandRateLimitOptions { MaxCommands = 1, Window = TimeSpan.FromSeconds(5) }, clock);

        Assert.True(limiter.TryAcquire(1, out _));
        Assert.False(limiter.TryAcquire(1, out _));

        // A different user has their own budget.
        Assert.True(limiter.TryAcquire(2, out _));
    }

    [Fact]
    public void NonPositiveBudget_DisablesThrottling()
    {
        var clock = new TestTimeProvider();
        var limiter = Create(new CommandRateLimitOptions { MaxCommands = 0, Window = TimeSpan.FromSeconds(5) }, clock);

        for (var i = 0; i < 50; i++)
        {
            Assert.True(limiter.TryAcquire(1, out _));
        }
    }
}
