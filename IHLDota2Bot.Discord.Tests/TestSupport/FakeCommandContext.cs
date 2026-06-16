using IHLDota2Bot.Discord.Commands;

namespace IHLDota2Bot.Discord.Tests.TestSupport;

/// <summary>
/// In-memory <see cref="IDiscordCommandContext"/> for handler tests. Records every
/// reply and whether the source message was deleted, so tests can assert what the
/// handler said without a live Discord gateway.
/// </summary>
internal sealed class FakeCommandContext : IDiscordCommandContext
{
    public string[] Parts { get; init; } = [];
    public ulong? AuthorId { get; init; }
    public string? AuthorUsername { get; init; }
    public IReadOnlyList<ulong> MentionedUserIds { get; init; } = [];

    public ulong? FirstMentionedUserId => MentionedUserIds.Count > 0 ? MentionedUserIds[0] : null;

    public List<string> Replies { get; } = [];
    public bool SourceDeleted { get; private set; }

    public ulong? ResolveTargetUserId() => FirstMentionedUserId ?? AuthorId;

    public Task ReplyAsync(string message)
    {
        Replies.Add(message);
        return Task.CompletedTask;
    }

    public Task DeleteSourceMessageAsync()
    {
        SourceDeleted = true;
        return Task.CompletedTask;
    }

    /// <summary>The single reply posted, asserting exactly one was sent.</summary>
    public string SingleReply() => Assert.Single(Replies);
}
