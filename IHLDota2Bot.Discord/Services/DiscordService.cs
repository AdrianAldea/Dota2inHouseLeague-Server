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

        if (IsAdminAction(action) && !IsAdmin(userMessage))
        {
            await userMessage.Channel.SendMessageAsync("You don't have permission to use that command.");
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
            case DiscordAction.COMMANDS:
            case DiscordAction.HELP:
                await ShowCommandsAsync(userMessage, parts);
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
            case DiscordAction.BAN:
                await BanPlayerAsync(userMessage, parts);
                break;
            case DiscordAction.UNBAN:
                await UnbanPlayerAsync(userMessage);
                break;
            case DiscordAction.KICK:
                await KickPlayerAsync(userMessage);
                break;
            case DiscordAction.FORCE_START:
                await ForceStartAsync(userMessage);
                break;
            case DiscordAction.RESET_QUEUE:
                await ResetQueueAsync(userMessage);
                break;
            case DiscordAction.ADD_ACCOUNT:
                await AddSteamAccountAsync(userMessage, parts);
                break;
            case DiscordAction.POOL_STATUS:
                await ShowPoolStatusAsync(userMessage);
                break;
            case DiscordAction.RANK:
                await ShowRankAsync(userMessage);
                break;
            case DiscordAction.VERSION:
                await ShowVersionAsync(userMessage);
                break;
            case DiscordAction.SCORE:
                await ShowMatchScoreAsync(userMessage, parts);
                break;
        }
    }

    private async Task ShowRankAsync(SocketUserMessage message)
    {
        var target = ResolveTargetUserId(message);
        if (target is null)
        {
            await message.Channel.SendMessageAsync("Could not resolve target user.");
            return;
        }

        var rank = await steamClient.GetPlayerRankAsync(target.Value);
        if (rank == null)
        {
            await message.Channel.SendMessageAsync("Player is not registered.");
            return;
        }

        await message.Channel.SendMessageAsync(MessagesFormatter.FormatRank(rank));
    }

    private async Task ShowVersionAsync(SocketUserMessage message)
    {
        var asm = typeof(DiscordService).Assembly;
        var version = asm.GetName().Version?.ToString() ?? "unknown";
        var commit = Environment.GetEnvironmentVariable("GIT_COMMIT") ?? "unknown";
        await message.Channel.SendMessageAsync($"**IHLDota2Bot**\nVersion: `{version}`\nCommit: `{commit}`");
    }

    private async Task ShowMatchScoreAsync(SocketUserMessage message, string[] parts)
    {
        if (parts.Length < 2 || !Guid.TryParse(parts[1], out var matchId))
        {
            await message.Channel.SendMessageAsync("Usage: `!score <matchId>`");
            return;
        }

        var match = await steamClient.GetMatchByIdAsync(matchId);
        if (match == null)
        {
            await message.Channel.SendMessageAsync("Match not found.");
            return;
        }

        await message.Channel.SendMessageAsync(MessagesFormatter.FormatMatch(match, message.Author?.Id ?? 0));
    }

    private static bool IsAdminAction(DiscordAction action) => action is
        DiscordAction.BAN or
        DiscordAction.UNBAN or
        DiscordAction.KICK or
        DiscordAction.FORCE_START or
        DiscordAction.RESET_QUEUE or
        DiscordAction.ADD_ACCOUNT or
        DiscordAction.POOL_STATUS;

    private bool IsAdmin(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return false;

        if (options.AdminUserIds.Contains(author.Id)) return true;

        if (!string.IsNullOrWhiteSpace(options.AdminRoleName) && author is SocketGuildUser guildUser)
        {
            return guildUser.Roles.Any(r => string.Equals(r.Name, options.AdminRoleName, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private async Task BanPlayerAsync(SocketUserMessage message, string[] parts)
    {
        var target = message.MentionedUsers?.FirstOrDefault();
        if (target == null)
        {
            await message.Channel.SendMessageAsync("Usage: `!ban @user [reason]`");
            return;
        }

        // Reason = everything after the mention. Easiest: skip parts[0] (cmd) and parts[1] (mention raw).
        var reason = parts.Length > 2 ? string.Join(' ', parts.Skip(2)) : null;

        var result = await steamClient.BanPlayerAsync(target.Id, reason);
        await ReplyIfFailedAsync(message, result, "Failed to ban player.");
    }

    private async Task UnbanPlayerAsync(SocketUserMessage message)
    {
        var target = message.MentionedUsers?.FirstOrDefault();
        if (target == null)
        {
            await message.Channel.SendMessageAsync("Usage: `!unban @user`");
            return;
        }

        var result = await steamClient.UnbanPlayerAsync(target.Id);
        await ReplyIfFailedAsync(message, result, "Failed to unban player.");
    }

    private async Task KickPlayerAsync(SocketUserMessage message)
    {
        var target = message.MentionedUsers?.FirstOrDefault();
        if (target == null)
        {
            await message.Channel.SendMessageAsync("Usage: `!kick @user`");
            return;
        }

        var result = await steamClient.KickPlayerAsync(target.Id);
        await ReplyIfFailedAsync(message, result, "Failed to kick player.");
    }

    private async Task ForceStartAsync(SocketUserMessage message)
    {
        var result = await steamClient.ForceStartLobbyAsync();
        await ReplyIfFailedAsync(message, result, "Failed to force start.");
    }

    private async Task ResetQueueAsync(SocketUserMessage message)
    {
        var result = await steamClient.ResetQueueAsync();
        await ReplyIfFailedAsync(message, result, "Failed to reset queue.");
    }

    private async Task AddSteamAccountAsync(SocketUserMessage message, string[] parts)
    {
        if (parts.Length < 3)
        {
            await message.Channel.SendMessageAsync("Usage: `!add_account <username> <password>` (delete the message after — it contains a password)");
            return;
        }

        var result = await steamClient.AddSteamAccountAsync(parts[1], parts[2]);
        await message.Channel.SendMessageAsync(result?.Message ?? "Failed to add account.");

        // Try to delete the original message so the password doesn't sit in chat history.
        try { await message.DeleteAsync(); }
        catch (Exception ex) { logger.LogWarning(ex, "Could not delete add_account message"); }
    }

    private async Task ShowPoolStatusAsync(SocketUserMessage message)
    {
        var status = await steamClient.GetPoolStatusAsync();
        if (status == null)
        {
            await message.Channel.SendMessageAsync("Failed to fetch pool status.");
            return;
        }

        await message.Channel.SendMessageAsync(MessagesFormatter.FormatPoolStatus(status));
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
        await ReplyIfFailedAsync(message, result, "Failed to send challenge.");
    }

    private async Task AcceptChallengeAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var result = await steamClient.AcceptChallengeAsync(author.Id);
        await ReplyIfFailedAsync(message, result, "Failed to accept challenge.");
    }

    private async Task DeclineChallengeAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var result = await steamClient.DeclineChallengeAsync(author.Id);
        await ReplyIfFailedAsync(message, result, "Failed to decline challenge.");
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
        await ReplyIfFailedAsync(message, result, "Failed to report match.");
    }

    private static ulong? ResolveTargetUserId(SocketUserMessage message)
    {
        // First mention wins; otherwise fall back to the message author
        var mentioned = message.MentionedUsers?.FirstOrDefault();
        return mentioned?.Id ?? message.Author?.Id;
    }

    // The Game Manager already broadcasts every *successful* state-change to the
    // channel (lobby events, plus ban/report/challenge announcements with @mention
    // pings). Echoing the operation's reply here as well posted it twice — e.g.
    // "X joined the queue" appearing once from the broadcast and once from the
    // reply. So for state-changing commands we surface the reply ONLY on failure;
    // success is announced by the broadcast, keeping exactly one message per action.
    private static Task ReplyIfFailedAsync(SocketUserMessage message, QueueOperationResponse? result, string fallback) =>
        result is { Success: true }
            ? Task.CompletedTask
            : message.Channel.SendMessageAsync(result?.Message ?? fallback);

    private static Task ReplyIfFailedAsync(SocketUserMessage message, ChallengeOperationResponse? result, string fallback) =>
        result is { Success: true }
            ? Task.CompletedTask
            : message.Channel.SendMessageAsync(result?.Message ?? fallback);

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
        await ReplyIfFailedAsync(message, result, "Failed to unregister.");
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
        await ReplyIfFailedAsync(message, result, "Failed to join queue.");
    }

    private async Task LeaveQueueAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var result = await steamClient.LeaveQueueAsync(author.Id);
        await ReplyIfFailedAsync(message, result, "Failed to leave queue.");
    }

    private async Task MarkReadyAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var result = await steamClient.MarkReadyAsync(author.Id);
        await ReplyIfFailedAsync(message, result, "Failed to mark ready.");
    }

    private async Task MarkUnreadyAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var result = await steamClient.MarkUnreadyAsync(author.Id);
        await ReplyIfFailedAsync(message, result, "Failed to mark unready.");
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

        // /player/{id} used to be a separate shape; it now returns PlayerStatsResponse
        // (same data as !mystats / !stats @user). Reuse the same formatter so the
        // !profile and !stats output stays in sync.
        var stats = await steamClient.GetPlayerStatsAsync(author.Id);
        if (stats == null)
        {
            await message.Channel.SendMessageAsync("Profile not found. Register with `!register <steamId>` first.");
            return;
        }

        await message.Channel.SendMessageAsync(MessagesFormatter.FormatPlayerStats(stats));
    }

    private static async Task ShowCommandsAsync(SocketUserMessage message, string[] parts)
    {
        if (parts.Length >= 2)
        {
            var lookup = parts[1].TrimStart('!').ToLowerInvariant();
            if (CommandHelpText.TryGet(lookup, out var detail))
            {
                await message.Channel.SendMessageAsync(detail);
                return;
            }

            await message.Channel.SendMessageAsync($"No help available for `!{lookup}`. Try `!help` for the full list.");
            return;
        }

        var commands = string.Join("\n", new[]
        {
            "**Available commands** — use `!help <command>` for details",
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
            "`!rank` / `!rank @user` — leaderboard rank + percentile",
            "`!score <matchId>` — show a finished match by id",
            "`!ping` — bot health and gateway latency",
            "`!version` — bot version + build sha",
            "`!commands` / `!help` — show this list (`!help <command>` for details)",
            "",
            "**Admin only**",
            "`!ban @user [reason]` · `!unban @user` · `!kick @user`",
            "`!force_start` · `!reset_queue` · `!pool_status`",
            "`!add_account <username> <password>` (delete the message after!)"
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
