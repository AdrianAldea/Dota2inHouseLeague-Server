using Discord.WebSocket;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>
/// <see cref="IDiscordCommandContext"/> backed by a <see cref="SocketSlashCommand"/>
/// interaction. The pipeline defers the interaction before invoking the handler
/// (handlers call the Game Manager and can exceed Discord's 3-second acknowledge
/// window), so replies go out as follow-ups. <see cref="HasReplied"/> lets the
/// pipeline close out "reply only on failure" handlers with a generic ✓ follow-up —
/// a deferred interaction that never receives one shows "thinking…" forever.
/// </summary>
public sealed class SlashCommandContext : IDiscordCommandContext
{
    private readonly SocketSlashCommand command;

    public SlashCommandContext(
        SocketSlashCommand command,
        string[] parts,
        IReadOnlyList<ulong> mentionedUserIds
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(mentionedUserIds);

        this.command = command;
        Parts = parts;
        MentionedUserIds = mentionedUserIds;
    }

    public string[] Parts { get; }

    public IReadOnlyList<ulong> MentionedUserIds { get; }

    public ulong? AuthorId => command.User?.Id;

    public string? AuthorUsername => command.User?.Username;

    public ulong? FirstMentionedUserId => MentionedUserIds.Count > 0 ? MentionedUserIds[0] : null;

    public ulong? ResolveTargetUserId() => FirstMentionedUserId ?? AuthorId;

    public bool HasReplied { get; private set; }

    public async Task ReplyAsync(string message)
    {
        HasReplied = true;
        await command.FollowupAsync(message);
    }

    /// <summary>
    /// No-op: slash-command arguments are only visible to the invoker, so there is
    /// no channel message carrying a secret to scrub (cf. text `!add_account`).
    /// </summary>
    public Task DeleteSourceMessageAsync() => Task.CompletedTask;
}
