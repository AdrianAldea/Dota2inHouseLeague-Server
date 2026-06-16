namespace IHLDota2Bot.Discord.Commands;

/// <summary>
/// The information a command handler needs about the invoking message, plus the
/// ways it can reply. Abstracted (rather than handing handlers the raw
/// <c>SocketUserMessage</c>) so handlers can be unit-tested against a fake context
/// with a mocked <c>ISteamClient</c> — Discord.Net's socket types are sealed and
/// awkward to construct in tests.
/// </summary>
public interface IDiscordCommandContext
{
    /// <summary>Whitespace-split message words, including the command itself at index 0.</summary>
    string[] Parts { get; }

    /// <summary>The author's Discord user id, or null if it couldn't be resolved.</summary>
    ulong? AuthorId { get; }

    /// <summary>The author's Discord username, or null if it couldn't be resolved.</summary>
    string? AuthorUsername { get; }

    /// <summary>The first @mentioned user id, or null if the message mentions no one.</summary>
    ulong? FirstMentionedUserId { get; }

    /// <summary>All @mentioned user ids in message order.</summary>
    IReadOnlyList<ulong> MentionedUserIds { get; }

    /// <summary>The first @mentioned user, falling back to the author when none is mentioned.</summary>
    ulong? ResolveTargetUserId();

    /// <summary>Posts a reply to the channel the command was issued in.</summary>
    Task ReplyAsync(string message);

    /// <summary>Deletes the invoking message (used to scrub secrets, e.g. <c>!add_account</c>).</summary>
    Task DeleteSourceMessageAsync();
}
