using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.Discord.Enums;
using IHLDota2Bot.Discord.Helpers;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>`!register <steamId>` — link the author's Discord account to a Steam ID.</summary>
public sealed class RegisterCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.REGISTER;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.Parts.Length < 2)
        {
            await context.ReplyAsync(CommandMessages.RegisterUsage);
            return;
        }

        if (!ulong.TryParse(context.Parts[1], out var steamId))
        {
            await context.ReplyAsync(CommandMessages.InvalidSteamId);
            return;
        }

        if (context.AuthorId is not { } authorId) return;

        var request = new RegisterPlayerRequest
        {
            SteamId = steamId,
            UserId = authorId,
            Username = context.AuthorUsername ?? string.Empty
        };
        var registeredPlayer = await steamClient.RegisterPlayerAsync(request);

        if (registeredPlayer == null)
        {
            await context.ReplyAsync(CommandMessages.RegisterFailed);
            return;
        }

        await context.ReplyAsync(
            CommandMessages.RegisterSuccess(registeredPlayer.Username, registeredPlayer.SteamId));
    }
}

/// <summary>`!link <steamId>` — update the Steam ID on the author's existing registration.</summary>
public sealed class LinkCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.LINK;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.Parts.Length < 2)
        {
            await context.ReplyAsync(CommandMessages.LinkUsage);
            return;
        }

        if (!ulong.TryParse(context.Parts[1], out var steamId))
        {
            await context.ReplyAsync(CommandMessages.InvalidSteamId);
            return;
        }

        if (context.AuthorId is not { } authorId) return;

        var result = await steamClient.LinkSteamIdAsync(authorId, steamId);
        // Link confirms on success (no Game Manager broadcast for it), so unlike the
        // queue/match commands we always surface the API's message.
        await context.ReplyAsync(result?.Message ?? CommandMessages.LinkFailed);
    }
}

/// <summary>`!unregister` — remove the author from the league.</summary>
public sealed class UnregisterCommandHandler(ISteamClient steamClient) : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.UNREGISTER;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.AuthorId is not { } authorId) return;

        var result = await steamClient.UnregisterAsync(authorId);
        await CommandReplies.ReplyIfFailedAsync(context, result?.Success, result?.Message, CommandMessages.UnregisterFailed);
    }
}
