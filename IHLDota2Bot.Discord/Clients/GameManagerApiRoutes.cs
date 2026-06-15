using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot.Discord.Clients;

/// <summary>
/// Single source of truth for the Game Manager HTTP API paths the Discord bot
/// calls. Centralised here (rather than scattered as inline string literals in
/// <see cref="SteamClient"/>) so a route change is a one-line edit and the paths
/// can be referenced from tests.
/// </summary>
internal static class GameManagerApiRoutes
{
    private const string Base = "api/gamemanager";

    public const string Register = $"{Base}/register";
    public static string Queue(ulong id) => $"{Base}/queue/{id}";
    public static string Leave(ulong id) => $"{Base}/leave/{id}";
    public static string Ready(ulong id) => $"{Base}/ready/{id}";
    public static string Unready(ulong id) => $"{Base}/unready/{id}";
    public static string Unregister(ulong id) => $"{Base}/unregister/{id}";
    public static string Link(ulong userId, ulong steamId) => $"{Base}/link/{userId}/{steamId}";

    public static string Stats(ulong id) => $"{Base}/stats/{id}";
    public static string Leaderboard(int top) => $"{Base}/leaderboard?top={top}";
    public static string Rank(ulong id) => $"{Base}/rank/{id}";

    public static string CurrentMatch(ulong id) => $"{Base}/match/current/{id}";
    public static string RecentMatches(ulong id, int top) => $"{Base}/match/recent/{id}?top={top}";
    public static string ReportMatch(ulong id, MatchTeam winner) => $"{Base}/match/report/{id}/{winner}";
    public static string MatchById(Guid matchId) => $"{Base}/match/{matchId}";

    public const string QueueStatus = $"{Base}/queue/status";

    public static string Challenge(ulong challengerId, ulong challengedId) => $"{Base}/challenge/{challengerId}/{challengedId}";
    public static string AcceptChallenge(ulong userId) => $"{Base}/challenge/accept/{userId}";
    public static string DeclineChallenge(ulong userId) => $"{Base}/challenge/decline/{userId}";

    public static string Ban(ulong id) => $"{Base}/admin/ban/{id}";
    public static string Unban(ulong id) => $"{Base}/admin/unban/{id}";
    public static string Kick(ulong id) => $"{Base}/admin/kick/{id}";
    public const string ForceStart = $"{Base}/admin/force_start";
    public const string ResetQueue = $"{Base}/admin/reset_queue";
    public const string AddAccount = $"{Base}/admin/add_account";
    public const string PoolStatus = $"{Base}/admin/pool_status";
}
