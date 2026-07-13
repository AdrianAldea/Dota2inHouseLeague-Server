using System.Collections.Concurrent;
using IHLDota2Bot.Discord.Configuration;
using Microsoft.Extensions.Options;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>
/// Thread-safe per-user sliding-window rate limiter. Keeps the timestamps of each
/// user's recent commands and rejects once they exceed the configured budget within
/// the window. Uses <see cref="TimeProvider"/> so the time-based behavior is testable.
/// </summary>
public sealed class CommandRateLimiter
{
    private readonly CommandRateLimitOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<ulong, Queue<long>> hits = new();

    public CommandRateLimiter(IOptions<CommandRateLimitOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.options = options.Value;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Tries to admit one command for <paramref name="userId"/>. Returns true and
    /// records the hit when within budget; otherwise returns false and reports how
    /// long until the oldest hit ages out of the window.
    /// </summary>
    public bool TryAcquire(ulong userId, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        // Non-positive budget disables throttling entirely.
        if (options.MaxCommands <= 0)
            return true;

        var now = timeProvider.GetUtcNow().UtcTicks;
        var windowTicks = options.Window.Ticks;
        var queue = hits.GetOrAdd(userId, static _ => new Queue<long>());

        lock (queue)
        {
            // Drop timestamps that have aged out of the window.
            while (queue.Count > 0 && now - queue.Peek() >= windowTicks)
            {
                queue.Dequeue();
            }

            if (queue.Count >= options.MaxCommands)
            {
                var oldest = queue.Peek();
                var remaining = windowTicks - (now - oldest);
                retryAfter = remaining > 0 ? TimeSpan.FromTicks(remaining) : TimeSpan.Zero;
                return false;
            }

            queue.Enqueue(now);
            return true;
        }
    }
}
