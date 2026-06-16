using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.Discord.Enums;
using IHLDota2Bot.Discord.Helpers;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>
/// Read-only query commands that fetch from the Game Manager and format a reply.
/// They mutate no state, so each is a thin handler over <see cref="ISteamClient"/>.
/// </summary>

/// <summary>`!status` — current queue/lobby state.</summary>
public sealed class QueueStatusCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.STATUS;

    public async Task HandleAsync(DiscordCommandContext context)
    {
        var status = await steamClient.GetQueueStatusAsync();
        if (status == null)
        {
            await context.Channel.SendMessageAsync("Failed to fetch queue status.");
            return;
        }

        await context.Channel.SendMessageAsync(MessagesFormatter.FormatQueueStatus(status));
    }
}

/// <summary>`!profile` — the author's profile (same data as !mystats).</summary>
public sealed class ProfileCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.PROFILE;

    public async Task HandleAsync(DiscordCommandContext context)
    {
        var author = context.Message.Author;
        if (author == null) return;

        var stats = await steamClient.GetPlayerStatsAsync(author.Id);
        if (stats == null)
        {
            await context.Channel.SendMessageAsync("Profile not found. Register with `!register <steamId>` first.");
            return;
        }

        await context.Channel.SendMessageAsync(MessagesFormatter.FormatPlayerStats(stats));
    }
}

/// <summary>`!mystats` — the author's rating / W-L.</summary>
public sealed class MyStatsCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.MYSTATS;

    public Task HandleAsync(DiscordCommandContext context) =>
        StatsReply.ShowStatsAsync(steamClient, context, context.Message.Author?.Id);
}

/// <summary>`!stats @user` — stats for a mentioned player (or the author).</summary>
public sealed class StatsCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.STATS;

    public Task HandleAsync(DiscordCommandContext context) =>
        StatsReply.ShowStatsAsync(steamClient, context, context.ResolveTargetUserId());
}

/// <summary>Shared stats lookup used by both !mystats and !stats.</summary>
internal static class StatsReply
{
    public static async Task ShowStatsAsync(ISteamClient steamClient, DiscordCommandContext context, ulong? userId)
    {
        if (userId is null)
        {
            await context.Channel.SendMessageAsync("Could not resolve target user.");
            return;
        }

        var stats = await steamClient.GetPlayerStatsAsync(userId.Value);
        if (stats == null)
        {
            await context.Channel.SendMessageAsync("Player is not registered.");
            return;
        }

        await context.Channel.SendMessageAsync(MessagesFormatter.FormatPlayerStats(stats));
    }
}

/// <summary>`!leaderboard [N]` — top N (default 10, max 50).</summary>
public sealed class LeaderboardCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.LEADERBOARD;

    public async Task HandleAsync(DiscordCommandContext context)
    {
        var top = 10;
        if (context.Parts.Length >= 2 && int.TryParse(context.Parts[1], out var parsed) && parsed > 0)
        {
            top = Math.Min(parsed, 50);
        }

        var leaderboard = await steamClient.GetLeaderboardAsync(top);
        if (leaderboard == null)
        {
            await context.Channel.SendMessageAsync("Failed to fetch leaderboard.");
            return;
        }

        await context.Channel.SendMessageAsync(MessagesFormatter.FormatLeaderboard(leaderboard));
    }
}

/// <summary>`!rank` / `!rank @user` — leaderboard rank + percentile.</summary>
public sealed class RankCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.RANK;

    public async Task HandleAsync(DiscordCommandContext context)
    {
        var target = context.ResolveTargetUserId();
        if (target is null)
        {
            await context.Channel.SendMessageAsync("Could not resolve target user.");
            return;
        }

        var rank = await steamClient.GetPlayerRankAsync(target.Value);
        if (rank == null)
        {
            await context.Channel.SendMessageAsync("Player is not registered.");
            return;
        }

        await context.Channel.SendMessageAsync(MessagesFormatter.FormatRank(rank));
    }
}

/// <summary>`!match` — the author's current active match.</summary>
public sealed class CurrentMatchCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.MATCH;

    public async Task HandleAsync(DiscordCommandContext context)
    {
        var author = context.Message.Author;
        if (author == null) return;

        var match = await steamClient.GetCurrentMatchAsync(author.Id);
        if (match == null)
        {
            await context.Channel.SendMessageAsync("You don't have an active match.");
            return;
        }

        await context.Channel.SendMessageAsync(MessagesFormatter.FormatMatch(match, author.Id));
    }
}

/// <summary>`!matches [N]` / `!matches @user [N]` — recent completed matches (default 5, max 20).</summary>
public sealed class RecentMatchesCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.MATCHES;

    public async Task HandleAsync(DiscordCommandContext context)
    {
        var top = 5;
        if (context.Parts.Length >= 2 && int.TryParse(context.Parts[1], out var parsed) && parsed > 0)
        {
            top = Math.Min(parsed, 20);
        }

        var targetUserId = context.ResolveTargetUserId();
        if (targetUserId is null)
        {
            await context.Channel.SendMessageAsync("Could not resolve target user.");
            return;
        }

        var recent = await steamClient.GetRecentMatchesAsync(targetUserId.Value, top);
        if (recent == null)
        {
            await context.Channel.SendMessageAsync("Failed to fetch recent matches.");
            return;
        }

        await context.Channel.SendMessageAsync(MessagesFormatter.FormatRecentMatches(recent, targetUserId.Value));
    }
}

/// <summary>`!score &lt;matchId&gt;` — show a finished match by id.</summary>
public sealed class MatchScoreCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.SCORE;

    public async Task HandleAsync(DiscordCommandContext context)
    {
        if (context.Parts.Length < 2 || !Guid.TryParse(context.Parts[1], out var matchId))
        {
            await context.Channel.SendMessageAsync("Usage: `!score <matchId>`");
            return;
        }

        var match = await steamClient.GetMatchByIdAsync(matchId);
        if (match == null)
        {
            await context.Channel.SendMessageAsync("Match not found.");
            return;
        }

        await context.Channel.SendMessageAsync(MessagesFormatter.FormatMatch(match, context.Message.Author?.Id ?? 0));
    }
}

/// <summary>`!pool_status` — Steam bot pool status (admin).</summary>
public sealed class PoolStatusCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.POOL_STATUS;

    public async Task HandleAsync(DiscordCommandContext context)
    {
        var status = await steamClient.GetPoolStatusAsync();
        if (status == null)
        {
            await context.Channel.SendMessageAsync("Failed to fetch pool status.");
            return;
        }

        await context.Channel.SendMessageAsync(MessagesFormatter.FormatPoolStatus(status));
    }
}
