using System.Text;
using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot.Discord.Helpers;

public static class MessagesFormatter
{
    public static string FormatPlayerStats(PlayerStatsResponse stats)
    {
        var winRate = stats.MatchesPlayed == 0 ? "—" : $"{stats.WinRate * 100:F1}%";
        return $"**{stats.Username}**\n" +
               $"- SteamID: `{stats.SteamId}`\n" +
               $"- Rating: `{stats.Rating}`\n" +
               $"- Matches: `{stats.MatchesPlayed}` (W `{stats.Wins}` / L `{stats.Losses}`)\n" +
               $"- Win rate: `{winRate}`";
    }

    public static string FormatLeaderboard(LeaderboardResponse leaderboard)
    {
        if (leaderboard.Entries.Count == 0)
            return "_No players on the leaderboard yet._";

        var sb = new StringBuilder();
        sb.AppendLine($"**Leaderboard — top {leaderboard.Entries.Count}**");
        foreach (var entry in leaderboard.Entries)
        {
            var winRate = entry.MatchesPlayed == 0 ? "—" : $"{entry.WinRate * 100:F0}%";
            sb.AppendLine($"`#{entry.Rank,2}` **{entry.Username}** · `{entry.Rating}` MMR · `{entry.Wins}-{entry.Losses}` ({winRate})");
        }
        return sb.ToString();
    }

    public static string FormatMatch(MatchSummary match, ulong viewerUserId)
    {
        var sb = new StringBuilder();
        var viewerEntry = match.Players.FirstOrDefault(p => p.UserId == viewerUserId);
        var viewerTeam = viewerEntry?.Team;

        sb.AppendLine($"**Match `{match.Id}`** — {match.Status}");
        sb.AppendLine($"Created: `{match.CreatedAt:yyyy-MM-dd HH:mm}` UTC");
        if (match.CompletedAt.HasValue)
            sb.AppendLine($"Completed: `{match.CompletedAt.Value:yyyy-MM-dd HH:mm}` UTC · Winner: **{match.WinningTeam}**");
        if (viewerTeam.HasValue)
            sb.AppendLine($"Your team: **{viewerTeam}**");

        sb.AppendLine();
        sb.AppendLine("**Radiant**");
        foreach (var p in match.Players.Where(p => p.Team == MatchTeam.Radiant))
            sb.AppendLine($"- {p.Username}");

        sb.AppendLine("**Dire**");
        foreach (var p in match.Players.Where(p => p.Team == MatchTeam.Dire))
            sb.AppendLine($"- {p.Username}");

        return sb.ToString();
    }

    public static string FormatRecentMatches(RecentMatchesResponse recent, ulong viewerUserId)
    {
        if (recent.Count == 0)
            return "_No completed matches yet._";

        var sb = new StringBuilder();
        sb.AppendLine($"**Recent matches** (showing {recent.Count})");
        foreach (var match in recent.Matches)
        {
            var me = match.Players.FirstOrDefault(p => p.UserId == viewerUserId);
            var outcome = me == null ? "—" : me.Won ? "WIN" : "LOSS";
            var completed = match.CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "in progress";
            sb.AppendLine($"`{completed}` — **{outcome}** ({me?.Team}) · Winner: {match.WinningTeam}");
        }
        return sb.ToString();
    }

    public static string FormatQueueStatus(QueueStatusResponse status)
    {
        var sb = new StringBuilder();
        sb.AppendLine("**Queue status**");
        sb.AppendLine($"Lobby: `{status.CurrentLobbyPlayerCount}/{status.PlayersPerLobby}` · Ready: `{status.PlayersReady}/{status.CurrentLobbyPlayerCount}`");
        sb.AppendLine($"Registered: `{status.TotalRegistered}` · Waiting (not in lobby): `{status.WaitingPlayers}`");

        if (status.Players.Count == 0)
        {
            sb.AppendLine("_No players in the current lobby._");
        }
        else
        {
            foreach (var player in status.Players)
            {
                var marker = player.IsReady ? "[R]" : "[ ]";
                sb.AppendLine($"`{marker}` {player.Username}");
            }
        }

        return sb.ToString();
    }


    public static string FormatProfile(string username, string steamId, int rating, int matches, int wins)
    {
        return $"**Profile for {username}**\n" +
               $"- SteamID: {steamId}\n" +
               $"- Rating: {rating}\n" +
               $"- Matches: {matches}\n" +
               $"- Wins: {wins}";
    }

    internal static string FormatCreateLobby(string lobbyName, string lobbyId)
    {
        return $"**Lobby Created: {lobbyName}**\n" +
               $"- Lobby ID: {lobbyId}";
    }

    internal static string FormatRegisterPlayer(string userid, string steamId)
    {
        return $"**User Registered: {userid}**\n" +
               $"- Steam ID: {steamId}";
    }
}
