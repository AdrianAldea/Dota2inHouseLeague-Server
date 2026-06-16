namespace IHLDota2Bot.Discord.Configuration;

/// <summary>
/// Per-user throttle for Discord commands: at most <see cref="MaxCommands"/> commands
/// per <see cref="Window"/>. Set <see cref="MaxCommands"/> to 0 (or less) to disable.
/// </summary>
public sealed class CommandRateLimitOptions
{
    public const string SectionName = "Discord:RateLimit";

    public int MaxCommands { get; set; } = 5;

    public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(5);
}
