using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.Discord.Enums;
using IHLDota2Bot.Discord.Helpers;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>
/// Admin-only commands. Each sets <see cref="IDiscordCommandHandler.RequiresAdmin"/>
/// so the dispatch pipeline enforces the permission check before invoking it.
/// </summary>

/// <summary>`!ban @user [reason]` — ban the mentioned player.</summary>
public sealed class BanCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.BAN;
    public bool RequiresAdmin => true;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.FirstMentionedUserId is not { } targetId)
        {
            await context.ReplyAsync(CommandMessages.BanUsage);
            return;
        }

        // Reason = everything after the command word and the mention raw token.
        var reason = context.Parts.Length > 2 ? string.Join(' ', context.Parts.Skip(2)) : null;

        var result = await steamClient.BanPlayerAsync(targetId, reason);
        await CommandReplies.ReplyIfFailedAsync(context, result?.Success, result?.Message, CommandMessages.BanFailed);
    }
}

/// <summary>`!unban @user` — clear a player's ban.</summary>
public sealed class UnbanCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.UNBAN;
    public bool RequiresAdmin => true;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.FirstMentionedUserId is not { } targetId)
        {
            await context.ReplyAsync(CommandMessages.UnbanUsage);
            return;
        }

        var result = await steamClient.UnbanPlayerAsync(targetId);
        await CommandReplies.ReplyIfFailedAsync(context, result?.Success, result?.Message, CommandMessages.UnbanFailed);
    }
}

/// <summary>`!kick @user` — remove the mentioned player from their lobby.</summary>
public sealed class KickCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.KICK;
    public bool RequiresAdmin => true;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.FirstMentionedUserId is not { } targetId)
        {
            await context.ReplyAsync(CommandMessages.KickUsage);
            return;
        }

        var result = await steamClient.KickPlayerAsync(targetId);
        await CommandReplies.ReplyIfFailedAsync(context, result?.Success, result?.Message, CommandMessages.KickFailed);
    }
}

/// <summary>`!force_start` — mark the current lobby ready and create a match.</summary>
public sealed class ForceStartCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.FORCE_START;
    public bool RequiresAdmin => true;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        var result = await steamClient.ForceStartLobbyAsync();
        await CommandReplies.ReplyIfFailedAsync(context, result?.Success, result?.Message, CommandMessages.ForceStartFailed);
    }
}

/// <summary>`!reset_queue` — empty the current lobby and release players.</summary>
public sealed class ResetQueueCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.RESET_QUEUE;
    public bool RequiresAdmin => true;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        var result = await steamClient.ResetQueueAsync();
        await CommandReplies.ReplyIfFailedAsync(context, result?.Success, result?.Message, CommandMessages.ResetQueueFailed);
    }
}

/// <summary>`!add_account <username> <password>` — add a Steam account to the pool.</summary>
public sealed class AddAccountCommandHandler(ISteamClient steamClient, ILogger<AddAccountCommandHandler> logger)
    : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.ADD_ACCOUNT;
    public bool RequiresAdmin => true;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.Parts.Length < 3)
        {
            await context.ReplyAsync(CommandMessages.AddAccountUsage);
            return;
        }

        // Scrub the password from chat first — before the API round-trip — so the
        // plaintext credential sits in channel history for as short a time as possible.
        try { await context.DeleteSourceMessageAsync(); }
        catch (Exception ex) { logger.LogWarning(ex, "Could not delete add_account message"); }

        var result = await steamClient.AddSteamAccountAsync(context.Parts[1], context.Parts[2]);
        await context.ReplyAsync(result?.Message ?? CommandMessages.AddAccountFailed);
    }
}
