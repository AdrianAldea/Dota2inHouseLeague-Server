namespace IHLDota2Bot.Discord.Helpers;

/// <summary>
/// Single catalog for the bot's user-facing usage hints and error strings, so the
/// wording lives in one place instead of being scattered across handler bodies.
/// </summary>
public static class CommandMessages
{
    // Authorization / generic
    public const string NotAuthorized = "You don't have permission to use that command.";
    public const string CouldNotResolveUser = "Could not resolve target user.";
    public const string InvalidSteamId = "Invalid SteamID format.";
    public const string PlayerNotRegistered = "Player is not registered.";

    // Usage hints
    public const string RegisterUsage = "Usage: `!register <steamId>`";
    public const string LinkUsage = "Usage: `!link <steamId>`";
    public const string ReportUsage = "Usage: `!report radiant` | `!report dire`";
    public const string ReportWinnerInvalid = "Winner must be `radiant` or `dire`.";
    public const string ChallengeUsage = "Usage: `!challenge @user`";
    public const string BanUsage = "Usage: `!ban @user [reason]`";
    public const string UnbanUsage = "Usage: `!unban @user`";
    public const string KickUsage = "Usage: `!kick @user`";
    public const string ScoreUsage = "Usage: `!score <matchId>`";
    public const string AddAccountUsage =
        "Usage: `!add_account <username> <password>` (delete the message after — it contains a password)";

    // Failure fallbacks (shown only when the API returns no message of its own)
    public const string RegisterFailed = "Registration failed. Please try again later.";
    public const string LinkFailed = "Failed to link Steam ID.";
    public const string UnregisterFailed = "Failed to unregister.";
    public const string JoinQueueFailed = "Failed to join queue.";
    public const string LeaveQueueFailed = "Failed to leave queue.";
    public const string MarkReadyFailed = "Failed to mark ready.";
    public const string MarkUnreadyFailed = "Failed to mark unready.";
    public const string ReportFailed = "Failed to report match.";
    public const string ChallengeFailed = "Failed to send challenge.";
    public const string AcceptChallengeFailed = "Failed to accept challenge.";
    public const string DeclineChallengeFailed = "Failed to decline challenge.";
    public const string BanFailed = "Failed to ban player.";
    public const string UnbanFailed = "Failed to unban player.";
    public const string KickFailed = "Failed to kick player.";
    public const string ForceStartFailed = "Failed to force start.";
    public const string ResetQueueFailed = "Failed to reset queue.";
    public const string AddAccountFailed = "Failed to add account.";

    // Read-only lookups
    public const string ProfileNotFound = "Profile not found. Register with `!register <steamId>` first.";
    public const string QueueStatusFailed = "Failed to fetch queue status.";
    public const string LeaderboardFailed = "Failed to fetch leaderboard.";
    public const string NoActiveMatch = "You don't have an active match.";
    public const string RecentMatchesFailed = "Failed to fetch recent matches.";
    public const string MatchNotFound = "Match not found.";
    public const string PoolStatusFailed = "Failed to fetch pool status.";

    public static string RegisterSuccess(string username, string steamId) =>
        $"You have successfully registered as {username} (SteamID: {steamId})!";

    public static string NoHelpFor(string command) =>
        $"No help available for `!{command}`. Try `!help` for the full list.";
}
