using Discord.WebSocket;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>
/// Production <see cref="IDiscordCommandContext"/> backed by a live
/// <see cref="SocketUserMessage"/> from the Discord gateway.
/// </summary>
public sealed class DiscordCommandContext : IDiscordCommandContext
{
    public required SocketUserMessage Message { get; init; }
    public required string[] Parts { get; init; }

    public ulong? AuthorId => Message.Author?.Id;

    public string? AuthorUsername => Message.Author?.Username;

    public ulong? FirstMentionedUserId => Message.MentionedUsers?.FirstOrDefault()?.Id;

    public IReadOnlyList<ulong> MentionedUserIds =>
        Message.MentionedUsers?.Select(u => u.Id).ToList() ?? [];

    public ulong? ResolveTargetUserId() => FirstMentionedUserId ?? AuthorId;

    public Task ReplyAsync(string message) => Message.Channel.SendMessageAsync(message);

    public Task DeleteSourceMessageAsync() => Message.DeleteAsync();
}
