using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot;

/// <summary>
/// Centralised Discord-facing wording for lobby/queue lifecycle events. Keeping
/// the phrasing here lets <see cref="LobbyPoolManager"/> read as intent
/// ("broadcast PlayerJoined") instead of inline string building, and puts every
/// user-visible message in one place to tweak.
/// </summary>
internal static class LobbyMessages
{
    public static string NoAvailableAccounts(string lobbyId) =>
        $"No available Steam accounts for lobby {lobbyId}. Please wait...";

    public static string PersistFailed(string lobbyId, string accountUsername) =>
        $"⚠️ Lobby {lobbyId} could not be persisted to the database. Account {accountUsername} released; please retry.";

    public static string LaunchFailed(string lobbyId, string accountUsername) =>
        $"⚠️ Lobby {lobbyId} could not be created on Steam. Match cancelled and account {accountUsername} released; please retry.";

    public static string LobbyReady(
        string lobbyId,
        string lobbyName,
        string lobbyPassword,
        string accountUsername
    ) =>
        $"Lobby {lobbyId} is ready! Steam lobby `{lobbyName}` (password `{lobbyPassword}`). Assigned account: {accountUsername}";

    public static string LobbyCreated(string lobbyId, int playerCount) =>
        $"New lobby created: {lobbyId} ({playerCount} players)";

    public static string PlayerJoined(string username, int count, int capacity) =>
        $"{username} joined the queue ({count}/{capacity})";

    public static string PlayerLeft(string username, int count, int capacity) =>
        $"{username} left the queue ({count}/{capacity})";

    public static string PlayerReady(string username, int readyCount, int totalCount) =>
        $"{username} is ready! ({readyCount}/{totalCount})";

    public static string PlayerUnready(string username) => $"{username} is no longer ready";

    public static string PlayerKicked(string username, int count, int capacity) =>
        $"{username} was kicked from the lobby ({count}/{capacity})";

    public static string LobbyReset(int removedPlayerCount) =>
        $"Queue reset. {removedPlayerCount} player(s) removed from the current lobby.";

    public static string LobbyFinished(string lobbyId) =>
        $"Lobby {lobbyId} has finished. Players can now join the next lobby.";

    public static string LobbyFull(string lobbyId, int count, int capacity) =>
        $"Lobby {lobbyId} is full ({count}/{capacity}). Waiting for everyone to !ready.";

    public static string GameStarted(string lobbyId) => $"GAME STARTED: {lobbyId}";

    public static string GameFinished(string lobbyId, string accountLabel) =>
        $"GAME FINISHED: {lobbyId} | Account {accountLabel} released";

    /// <summary>Fallback line when the GC reported a result but the DB write produced no match row.</summary>
    public static string MatchFinishedFallback(
        string lobbyId,
        MatchTeam? winningTeam,
        ulong dotaMatchId
    )
    {
        var winnerText = winningTeam?.ToString() ?? "No winner (not scored)";
        return $"🏁 Match for lobby {lobbyId} finished. Winner: {winnerText} (Dota match {dotaMatchId}).";
    }

    /// <summary>Multi-line result card posted to Discord when a match completes.</summary>
    public static string MatchResult(MatchSummary match, ulong dotaMatchId)
    {
        var header = match.WinningTeam switch
        {
            MatchTeam.Radiant => "🏆 **Radiant Victory!**",
            MatchTeam.Dire => "🏆 **Dire Victory!**",
            _ => "🏁 **Match finished** (not scored)",
        };

        var radiant = match.Players.Where(p => p.Team == MatchTeam.Radiant).Select(p => p.Username);
        var dire = match.Players.Where(p => p.Team == MatchTeam.Dire).Select(p => p.Username);

        return string.Join(
            "\n",
            [
                header,
                $"Dota match `{dotaMatchId}`",
                $"**Radiant:** {string.Join(", ", radiant)}",
                $"**Dire:** {string.Join(", ", dire)}",
            ]
        );
    }
}
