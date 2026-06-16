using Discord.WebSocket;
using IHLDota2Bot.Discord.Enums;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>`!ping` — bot health and gateway latency.</summary>
public sealed class PingCommandHandler : IDiscordCommandHandler
{
    private readonly DiscordSocketClient discordClient;

    public PingCommandHandler(DiscordSocketClient discordClient)
    {
        ArgumentNullException.ThrowIfNull(discordClient);
        this.discordClient = discordClient;
    }

    public DiscordAction Action => DiscordAction.PING;

    public Task HandleAsync(IDiscordCommandContext context) =>
        context.ReplyAsync($"Pong! Gateway latency: {discordClient.Latency}ms");
}

/// <summary>`!version` — bot version + build sha.</summary>
public sealed class VersionCommandHandler : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.VERSION;

    public Task HandleAsync(IDiscordCommandContext context)
    {
        var asm = typeof(VersionCommandHandler).Assembly;
        var version = asm.GetName().Version?.ToString() ?? "unknown";
        var commit = Environment.GetEnvironmentVariable("GIT_COMMIT") ?? "unknown";
        return context.ReplyAsync($"**IHLDota2Bot**\nVersion: `{version}`\nCommit: `{commit}`");
    }
}
