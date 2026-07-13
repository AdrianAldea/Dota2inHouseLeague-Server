using IHLDota2Bot.Discord.Commands;

namespace IHLDota2Bot.Discord.Helpers;

/// <summary>
/// Shared reply helper for state-changing commands.
/// </summary>
public static class CommandReplies
{
    /// <summary>
    /// The Game Manager already broadcasts every *successful* state-change to the
    /// channel (lobby events, plus ban/report/challenge announcements with @mention
    /// pings). Echoing the operation's reply here as well posted it twice. So for
    /// state-changing commands we surface the reply ONLY on failure; success is
    /// announced by the broadcast, keeping exactly one message per action.
    /// <para>
    /// Works for any operation response exposing a success flag + message
    /// (e.g. <c>QueueOperationResponse</c>, <c>ChallengeOperationResponse</c>); a
    /// null response (transport failure) is treated as failure.
    /// </para>
    /// </summary>
    public static Task ReplyIfFailedAsync(
        IDiscordCommandContext context,
        bool? success,
        string? message,
        string fallback
    ) =>
        success == true
            ? Task.CompletedTask
            : context.ReplyAsync(string.IsNullOrWhiteSpace(message) ? fallback : message);
}
