using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.Discord.Enums;
using IHLDota2Bot.Discord.Helpers;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>`!report radiant|dire` — report the winner of the author's current match.</summary>
public sealed class ReportMatchCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.REPORT;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.Parts.Length < 2)
        {
            await context.ReplyAsync(CommandMessages.ReportUsage);
            return;
        }

        if (!Enum.TryParse<MatchTeam>(context.Parts[1], ignoreCase: true, out var winner))
        {
            await context.ReplyAsync(CommandMessages.ReportWinnerInvalid);
            return;
        }

        if (context.AuthorId is not { } authorId)
            return;

        var result = await steamClient.ReportMatchAsync(authorId, winner);
        await CommandReplies.ReplyIfFailedAsync(
            context,
            result?.Success,
            result?.Message,
            CommandMessages.ReportFailed
        );
    }
}

/// <summary>`!challenge @user` — send a 1v1 duel invite.</summary>
public sealed class ChallengeCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.CHALLENGE;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.AuthorId is not { } authorId)
            return;

        var opponent = context.MentionedUserIds.FirstOrDefault(id => id != authorId, 0UL);
        if (opponent == 0UL)
        {
            await context.ReplyAsync(CommandMessages.ChallengeUsage);
            return;
        }

        var result = await steamClient.ChallengePlayerAsync(authorId, opponent);
        await CommandReplies.ReplyIfFailedAsync(
            context,
            result?.Success,
            result?.Message,
            CommandMessages.ChallengeFailed
        );
    }
}

/// <summary>`!accept_match` — accept the author's latest pending challenge.</summary>
public sealed class AcceptChallengeCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.ACCEPT_MATCH;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.AuthorId is not { } authorId)
            return;

        var result = await steamClient.AcceptChallengeAsync(authorId);
        await CommandReplies.ReplyIfFailedAsync(
            context,
            result?.Success,
            result?.Message,
            CommandMessages.AcceptChallengeFailed
        );
    }
}

/// <summary>`!decline_match` — decline the author's latest pending challenge.</summary>
public sealed class DeclineChallengeCommandHandler(ISteamClient steamClient)
    : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.DECLINE_MATCH;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.AuthorId is not { } authorId)
            return;

        var result = await steamClient.DeclineChallengeAsync(authorId);
        await CommandReplies.ReplyIfFailedAsync(
            context,
            result?.Success,
            result?.Message,
            CommandMessages.DeclineChallengeFailed
        );
    }
}
