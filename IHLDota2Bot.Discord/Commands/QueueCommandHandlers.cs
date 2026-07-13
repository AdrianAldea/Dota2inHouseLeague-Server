using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.Discord.Enums;
using IHLDota2Bot.Discord.Helpers;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>
/// Queue/lobby membership commands. Each mutates server state and is announced by
/// the Game Manager broadcast on success, so they reply only on failure
/// (see <see cref="CommandReplies"/>).
/// </summary>
/// <summary>`!queue` / `!q` — join the matchmaking queue.</summary>
public sealed class JoinQueueCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.QUEUE;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.AuthorId is not { } authorId)
            return;

        var result = await steamClient.JoinQueueAsync(authorId);
        await CommandReplies.ReplyIfFailedAsync(
            context,
            result?.Success,
            result?.Message,
            CommandMessages.JoinQueueFailed
        );
    }
}

/// <summary>`!leave` — leave the current lobby/queue.</summary>
public sealed class LeaveQueueCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.LEAVE;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.AuthorId is not { } authorId)
            return;

        var result = await steamClient.LeaveQueueAsync(authorId);
        await CommandReplies.ReplyIfFailedAsync(
            context,
            result?.Success,
            result?.Message,
            CommandMessages.LeaveQueueFailed
        );
    }
}

/// <summary>`!ready` — mark the author ready in the current lobby.</summary>
public sealed class ReadyCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.READY;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.AuthorId is not { } authorId)
            return;

        var result = await steamClient.MarkReadyAsync(authorId);
        await CommandReplies.ReplyIfFailedAsync(
            context,
            result?.Success,
            result?.Message,
            CommandMessages.MarkReadyFailed
        );
    }
}

/// <summary>`!unready` — undo the author's ready state.</summary>
public sealed class UnreadyCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.UNREADY;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.AuthorId is not { } authorId)
            return;

        var result = await steamClient.MarkUnreadyAsync(authorId);
        await CommandReplies.ReplyIfFailedAsync(
            context,
            result?.Success,
            result?.Message,
            CommandMessages.MarkUnreadyFailed
        );
    }
}
