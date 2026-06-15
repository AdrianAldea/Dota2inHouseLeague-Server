using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;

namespace IHLDota2Bot.Discord.Clients;

/// <summary>
/// Typed client for the Game Manager HTTP API. Implemented by
/// <see cref="SteamClient"/>; <c>DiscordService</c> depends on this interface so
/// the command handlers can be unit-tested against a fake backend. Every method
/// returns <c>null</c> on transport/HTTP failure (the implementation logs the cause).
/// </summary>
public interface ISteamClient
{
    Task<RegisteredPlayer?> RegisterPlayerAsync(RegisterPlayerRequest request, CancellationToken cancellationToken = default);
    Task<QueueOperationResponse?> JoinQueueAsync(ulong id, CancellationToken cancellationToken = default);
    Task<QueueOperationResponse?> LeaveQueueAsync(ulong id, CancellationToken cancellationToken = default);
    Task<QueueOperationResponse?> MarkReadyAsync(ulong id, CancellationToken cancellationToken = default);
    Task<QueueOperationResponse?> MarkUnreadyAsync(ulong id, CancellationToken cancellationToken = default);
    Task<QueueOperationResponse?> UnregisterAsync(ulong id, CancellationToken cancellationToken = default);
    Task<QueueOperationResponse?> LinkSteamIdAsync(ulong userId, ulong steamId, CancellationToken cancellationToken = default);

    Task<PlayerStatsResponse?> GetPlayerStatsAsync(ulong id, CancellationToken cancellationToken = default);
    Task<LeaderboardResponse?> GetLeaderboardAsync(int top, CancellationToken cancellationToken = default);
    Task<RankResponse?> GetPlayerRankAsync(ulong id, CancellationToken cancellationToken = default);

    Task<MatchSummary?> GetCurrentMatchAsync(ulong id, CancellationToken cancellationToken = default);
    Task<MatchSummary?> GetMatchByIdAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<RecentMatchesResponse?> GetRecentMatchesAsync(ulong id, int top, CancellationToken cancellationToken = default);
    Task<QueueOperationResponse?> ReportMatchAsync(ulong id, MatchTeam winner, CancellationToken cancellationToken = default);

    Task<QueueStatusResponse?> GetQueueStatusAsync(CancellationToken cancellationToken = default);

    Task<ChallengeOperationResponse?> ChallengePlayerAsync(ulong challengerId, ulong challengedId, CancellationToken cancellationToken = default);
    Task<ChallengeOperationResponse?> AcceptChallengeAsync(ulong userId, CancellationToken cancellationToken = default);
    Task<ChallengeOperationResponse?> DeclineChallengeAsync(ulong userId, CancellationToken cancellationToken = default);

    Task<QueueOperationResponse?> BanPlayerAsync(ulong id, string? reason, CancellationToken cancellationToken = default);
    Task<QueueOperationResponse?> UnbanPlayerAsync(ulong id, CancellationToken cancellationToken = default);
    Task<QueueOperationResponse?> KickPlayerAsync(ulong id, CancellationToken cancellationToken = default);
    Task<QueueOperationResponse?> ForceStartLobbyAsync(CancellationToken cancellationToken = default);
    Task<QueueOperationResponse?> ResetQueueAsync(CancellationToken cancellationToken = default);
    Task<QueueOperationResponse?> AddSteamAccountAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<PoolStatusResponse?> GetPoolStatusAsync(CancellationToken cancellationToken = default);
}
