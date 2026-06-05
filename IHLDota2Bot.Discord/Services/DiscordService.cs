using Discord;
using Discord.WebSocket;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.Discord.Configuration;
using IHLDota2Bot.Discord.Enums;
using IHLDota2Bot.Discord.Helpers;
using Microsoft.Extensions.Options;

namespace IHLDota2Bot.Discord.Services;

public class DiscordService : IHostedService, IAsyncDisposable
{
    private readonly SteamClient steamClient;
    private readonly ILogger<DiscordService> logger;
    private readonly DiscordOptions options;
    private readonly DiscordSocketClient discordClient;
    private bool isRunning;

    public DiscordService(
        SteamClient steamClient,
        IOptions<DiscordOptions> options,
        ILogger<DiscordService> logger)
    {
        ArgumentNullException.ThrowIfNull(steamClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.steamClient = steamClient;
        this.options = options.Value;
        this.logger = logger;

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        };

        discordClient = new DiscordSocketClient(config);
        discordClient.MessageReceived += HandleMessageAsync;
        discordClient.Log += OnDiscordLogAsync;
    }

    private Task OnDiscordLogAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };
        logger.Log(level, message.Exception, "[Discord] {Source}: {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage) return;
        if (userMessage.Author?.IsBot ?? false) return;

        var content = userMessage.Content?.Trim();
        if (string.IsNullOrEmpty(content)) return;
        if (!content.StartsWith('!')) return;

        var parts = content[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var rawCommand = parts[0].ToUpperInvariant();
        var command = rawCommand switch
        {
            "Q" => "QUEUE",
            "HELP" => "COMMANDS",
            _ => rawCommand
        };

        if (!Enum.TryParse(command, out DiscordAction action))
        {
            logger.LogDebug("Unknown command received: {Command}", rawCommand);
            return;
        }

        logger.LogInformation("Received command: {Action}", action);

        switch (action)
        {
            case DiscordAction.REGISTER:
                await RegisterPlayerAsync(userMessage, parts);
                break;
            case DiscordAction.PROFILE:
                await ShowProfileAsync(userMessage);
                break;
            case DiscordAction.CREATE_LOBBY:
                await CreateLobbyAsync(userMessage);
                break;
            case DiscordAction.COMMANDS:
            case DiscordAction.HELP:
                await ShowCommandsAsync(userMessage);
                break;
            case DiscordAction.PING:
                await PingAsync(userMessage);
                break;
            case DiscordAction.QUEUE:
                await JoinQueueAsync(userMessage);
                break;
            case DiscordAction.LEAVE:
                await LeaveQueueAsync(userMessage);
                break;
            case DiscordAction.READY:
                await MarkReadyAsync(userMessage);
                break;
            case DiscordAction.UNREADY:
                await MarkUnreadyAsync(userMessage);
                break;
            case DiscordAction.STATUS:
                await ShowQueueStatusAsync(userMessage);
                break;
            case DiscordAction.UNREGISTER:
                await UnregisterAsync(userMessage);
                break;
            case DiscordAction.LINK:
                await LinkSteamIdAsync(userMessage, parts);
                break;
            case DiscordAction.MYSTATS:
                await ShowStatsAsync(userMessage, userMessage.Author?.Id);
                break;
            case DiscordAction.STATS:
                await ShowStatsAsync(userMessage, ResolveTargetUserId(userMessage));
                break;
            case DiscordAction.LEADERBOARD:
                await ShowLeaderboardAsync(userMessage, parts);
                break;
            case DiscordAction.MATCH:
                await ShowCurrentMatchAsync(userMessage);
                break;
            case DiscordAction.MATCHES:
                await ShowRecentMatchesAsync(userMessage, parts);
                break;
            case DiscordAction.REPORT:
                await ReportMatchAsync(userMessage, parts);
                break;
            case DiscordAction.CHALLENGE:
                await ChallengePlayerAsync(userMessage);
                break;
            case DiscordAction.ACCEPT_MATCH:
                await AcceptChallengeAsync(userMessage);
                break;
            case DiscordAction.DECLINE_MATCH:
                await DeclineChallengeAsync(userMessage);
                break;
        }
    }

    private async Task ChallengePlayerAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var opponent = message.MentionedUsers?.FirstOrDefault(u => u.Id != author.Id);
        if (opponent == null)
        {
            await message.Channel.SendMessageAsync("Usage: `!challenge @user`");
            return;
        }

        var result = await steamClient.ChallengePlayerAsync(author.Id, opponent.Id);
        await message.Channel.SendMessageAsync(result?.Message ?? "Failed to send challenge.");
    }

    private async Task AcceptChallengeAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var result = await steamClient.AcceptChallengeAsync(author.Id);
        await message.Channel.SendMessageAsync(result?.Message ?? "Failed to accept challenge.");
    }

    private async Task DeclineChallengeAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var result = await steamClient.DeclineChallengeAsync(author.Id);
        await message.Channel.SendMessageAsync(result?.Message ?? "Failed to decline challenge.");
    }

    private async Task ShowCurrentMatchAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var match = await steamClient.GetCurrentMatchAsync(author.Id);
        if (match == null)
        {
            await message.Channel.SendMessageAsync("You don't have an active match.");
            return;
        }

        await message.Channel.SendMessageAsync(MessagesFormatter.FormatMatch(match, author.Id));
    }

    private async Task ShowRecentMatchesAsync(SocketUserMessage message, string[] parts)
    {
        var top = 5;
        if (parts.Length >= 2 && int.TryParse(parts[1], out var parsed) && parsed > 0)
        {
            top = Math.Min(parsed, 20);
        }

        var targetUserId = ResolveTargetUserId(message);
        if (targetUserId is null)
        {
            await message.Channel.SendMessageAsync("Could not resolve target user.");
            return;
        }

        var recent = await steamClient.GetRecentMatchesAsync(targetUserId.Value, top);
        if (recent == null)
        {
            await message.Channel.SendMessageAsync("Failed to fetch recent matches.");
            return;
        }

        await message.Channel.SendMessageAsync(MessagesFormatter.FormatRecentMatches(recent, targetUserId.Value));
    }

    private async Task ReportMatchAsync(SocketUserMessage message, string[] parts)
    {
        if (parts.Length < 2)
        {
            await message.Channel.SendMessageAsync("Usage: `!report radiant` | `!report dire`");
            return;
        }

        if (!Enum.TryParse<MatchTeam>(parts[1], ignoreCase: true, out var winner))
        {
            await message.Channel.SendMessageAsync("Winner must be `radiant` or `dire`.");
            return;
        }

        var author = message.Author;
        if (author == null) return;

        var result = await steamClient.ReportMatchAsync(author.Id, winner);
        await message.Channel.SendMessageAsync(result?.Message ?? "Failed to report match.");
    }

    private static ulong? ResolveTargetUserId(SocketUserMessage message)
    {
        // First mention wins; otherwise fall back to the message author
        var mentioned = message.MentionedUsers?.FirstOrDefault();
        return mentioned?.Id ?? message.Author?.Id;
    }

    private async Task ShowStatsAsync(SocketUserMessage message, ulong? userId)
    {
        if (userId is null)
        {
            await message.Channel.SendMessageAsync("Could not resolve target user.");
            return;
        }

        var stats = await steamClient.GetPlayerStatsAsync(userId.Value);
        if (stats == null)
        {
            await message.Channel.SendMessageAsync("Player is not registered.");
            return;
        }

        await message.Channel.SendMessageAsync(MessagesFormatter.FormatPlayerStats(stats));
    }

    private async Task ShowLeaderboardAsync(SocketUserMessage message, string[] parts)
    {
        var top = 10;
        if (parts.Length >= 2 && int.TryParse(parts[1], out var parsed) && parsed > 0)
        {
            top = Math.Min(parsed, 50);
        }

        var leaderboard = await steamClient.GetLeaderboardAsync(top);
        if (leaderboard == null)
        {
            await message.Channel.SendMessageAsync("Failed to fetch leaderboard.");
            return;
        }

        await message.Channel.SendMessageAsync(MessagesFormatter.FormatLeaderboard(leaderboard));
    }

    private async Task UnregisterAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var result = await steamClient.UnregisterAsync(author.Id);
        await message.Channel.SendMessageAsync(result?.Message ?? "Failed to unregister.");
    }

    private async Task LinkSteamIdAsync(SocketUserMessage message, string[] parts)
    {
        if (parts.Length < 2)
        {
            await message.Channel.SendMessageAsync("Usage: `!link <steamId>`");
            return;
        }

        if (!ulong.TryParse(parts[1], out var steamId))
        {
            await message.Channel.SendMessageAsync("Invalid SteamID format.");
            return;
        }

        var author = message.Author;
        if (author == null) return;

        var result = await steamClient.LinkSteamIdAsync(author.Id, steamId);
        await message.Channel.SendMessageAsync(result?.Message ?? "Failed to link Steam ID.");
    }

    private async Task PingAsync(SocketUserMessage message)
    {
        await message.Channel.SendMessageAsync($"Pong! Gateway latency: {discordClient.Latency}ms");
    }

    private async Task JoinQueueAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var result = await steamClient.JoinQueueAsync(author.Id);
        await message.Channel.SendMessageAsync(result?.Message ?? "Failed to join queue.");
    }

    private async Task LeaveQueueAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var result = await steamClient.LeaveQueueAsync(author.Id);
        await message.Channel.SendMessageAsync(result?.Message ?? "Failed to leave queue.");
    }

    private async Task MarkReadyAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var result = await steamClient.MarkReadyAsync(author.Id);
        await message.Channel.SendMessageAsync(result?.Message ?? "Failed to mark ready.");
    }

    private async Task MarkUnreadyAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var result = await steamClient.MarkUnreadyAsync(author.Id);
        await message.Channel.SendMessageAsync(result?.Message ?? "Failed to mark unready.");
    }

    private async Task ShowQueueStatusAsync(SocketUserMessage message)
    {
        var status = await steamClient.GetQueueStatusAsync();
        if (status == null)
        {
            await message.Channel.SendMessageAsync("Failed to fetch queue status.");
            return;
        }

        await message.Channel.SendMessageAsync(MessagesFormatter.FormatQueueStatus(status));
    }

    private async Task RegisterPlayerAsync(SocketUserMessage message, string[] parts)
    {
        if (parts.Length < 2)
        {
            await message.Channel.SendMessageAsync("Usage: !register <steamId>");
            return;
        }

        if (!ulong.TryParse(parts[1], out var steamId))
        {
            await message.Channel.SendMessageAsync("Invalid SteamID format.");
            return;
        }

        var author = message.Author;
        if (author == null) return;

        var request = new RegisterPlayerRequest
        {
            SteamId = steamId,
            UserId = author.Id,
            Username = author.Username
        };
        var registeredPlayer = await steamClient.RegisterPlayerAsync(request);

        if (registeredPlayer == null)
        {
            await message.Channel.SendMessageAsync("Registration failed. Please try again later.");
            return;
        }

        await message.Channel.SendMessageAsync(
            $"You have successfully registered as {registeredPlayer.Username} (SteamID: {registeredPlayer.SteamId})!");
    }

    private async Task ShowProfileAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var profileResponse = await steamClient.ShowProfileAsync(author.Id);
        if (profileResponse == null)
        {
            await message.Channel.SendMessageAsync("Profile not found.");
            return;
        }

        var profile = MessagesFormatter.FormatProfile(
            profileResponse.Username,
            profileResponse.SteamId,
            profileResponse.Rating,
            profileResponse.Matches,
            profileResponse.Wins);

        await message.Channel.SendMessageAsync(profile);
    }

    private async Task CreateLobbyAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var lobbyResponse = await steamClient.CreateLobbyAsync(author.Id);
        if (lobbyResponse == null)
        {
            await message.Channel.SendMessageAsync("Failed to create lobby.");
            return;
        }

        var createLobbyMessage = MessagesFormatter.FormatCreateLobby(
            lobbyResponse.LobbyName,
            lobbyResponse.LobbyId);

        await message.Channel.SendMessageAsync(createLobbyMessage);
    }

    private static async Task ShowCommandsAsync(SocketUserMessage message)
    {
        var commands = string.Join("\n", new[]
        {
            "**Available commands**",
            "`!register <steamId>` — register your Steam account",
            "`!link <steamId>` — update your linked Steam ID",
            "`!unregister` — leave the league",
            "`!profile` — show your profile",
            "`!queue` / `!q` — join the matchmaking queue",
            "`!leave` — leave the queue",
            "`!ready` — mark yourself ready in the current lobby",
            "`!unready` — undo ready",
            "`!status` — show the current queue/lobby state",
            "`!mystats` — your rating / W-L",
            "`!stats @user` — stats for another player",
            "`!leaderboard [N]` — top N (default 10, max 50)",
            "`!match` — show your current active match",
            "`!matches [N]` / `!matches @user [N]` — recent completed matches (default 5)",
            "`!report radiant|dire` — report the winner of your current match",
            "`!challenge @user` — 1v1 duel invite (expires in 15 min)",
            "`!accept_match` — accept your latest pending challenge",
            "`!decline_match` — decline your latest pending challenge",
            "`!create_lobby` — create a new lobby",
            "`!ping` — bot health and gateway latency",
            "`!commands` / `!help` — show this list"
        });
        await message.Channel.SendMessageAsync(commands);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (isRunning) return;

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            logger.LogError("Discord token is not configured. Set Discord:Token in user-secrets.");
            return;
        }

        logger.LogInformation("Starting Discord service...");
        await discordClient.LoginAsync(TokenType.Bot, options.Token);
        await discordClient.StartAsync();
        isRunning = true;
    }

    public bool IsRunning => isRunning;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!isRunning) return;

        logger.LogInformation("Stopping Discord service...");
        await discordClient.LogoutAsync();
        await discordClient.StopAsync();
        isRunning = false;
    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (options.ChannelId == 0)
        {
            logger.LogWarning("Discord channel id is not configured; dropping message");
            return;
        }

        if (discordClient.GetChannel(options.ChannelId) is IMessageChannel channel)
        {
            await channel.SendMessageAsync(message);
        }
        else
        {
            logger.LogWarning("Discord channel {ChannelId} not found", options.ChannelId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await discordClient.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
