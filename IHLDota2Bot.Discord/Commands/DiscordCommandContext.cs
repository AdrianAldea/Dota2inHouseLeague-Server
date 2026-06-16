using Discord;
using Discord.WebSocket;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>
/// Per-message data handed to a command handler: the originating message and the
/// whitespace-split command tokens (the command word itself is at index 0).
/// </summary>
public sealed class DiscordCommandContext
{
    public required SocketUserMessage Message { get; init; }
    public required string[] Parts { get; init; }

    public IMessageChannel Channel => Message.Channel;

    /// <summary>First @mention if present, otherwise the message author.</summary>
    public ulong? ResolveTargetUserId() =>
        Message.MentionedUsers?.FirstOrDefault()?.Id ?? Message.Author?.Id;
}
