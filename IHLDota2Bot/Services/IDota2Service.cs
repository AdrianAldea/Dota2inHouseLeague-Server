using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;

namespace IHLDota2Bot.Services;

/// <summary>
/// Application-facing surface of the Game Manager: player/queue/match/challenge
/// operations plus the admin and pool controls the API endpoints expose. Endpoints
/// depend on this interface rather than the concrete <see cref="Dota2Service"/> so
/// the orchestration can be substituted or mocked in tests.
/// </summary>
public interface IDota2Service
{
    Task<bool> StartServerAsync();
    void InvitePlayersToLobby(IList<ulong> steamIds);
    Task CreateLobbyAsync(CreateLobbyRequest createLobbyRequest);

    Task<RegisteredPlayer> RegisterPlayerAsync(
        RegisterPlayerRequest request,
        CancellationToken cancellationToken = default
    );
    Task<PlayerStatsResponse?> GetPlayerProfileAsync(
        ulong playerId,
        CancellationToken cancellationToken = default
    );
    Task<QueueOperationResponse> MarkPlayerAsReadyAsync(ulong id);
    Task<QueueOperationResponse> MarkPlayerAsUnreadyAsync(ulong id);
    Task<QueueOperationResponse> JoinQueueAsync(
        ulong userId,
        CancellationToken cancellationToken = default
    );
    Task<QueueOperationResponse> LeaveQueueAsync(ulong userId);
    QueueStatusResponse GetQueueStatus();

    /// <summary>The given player's own queue membership/ready state.</summary>
    MyQueueStateResponse GetPlayerQueueState(ulong userId);
    Task<QueueOperationResponse> UnregisterPlayerAsync(
        ulong userId,
        CancellationToken cancellationToken = default
    );

    Task<PlayerStatsResponse?> GetPlayerStatsAsync(
        ulong userId,
        CancellationToken cancellationToken = default
    );
    Task<LeaderboardResponse> GetLeaderboardAsync(
        int topN,
        CancellationToken cancellationToken = default
    );
    Task<MatchSummary?> GetCurrentMatchAsync(
        ulong userId,
        CancellationToken cancellationToken = default
    );
    Task<MatchSummary?> GetMatchByIdAsync(
        Guid matchId,
        CancellationToken cancellationToken = default
    );
    Task<MatchSummary?> GetMatchByLobbyAsync(
        string lobbyId,
        CancellationToken cancellationToken = default
    );
    Task<RankResponse?> GetPlayerRankAsync(
        ulong userId,
        CancellationToken cancellationToken = default
    );
    Task<RecentMatchesResponse> GetRecentMatchesAsync(
        ulong userId,
        int top,
        CancellationToken cancellationToken = default
    );
    Task<QueueOperationResponse> ReportMatchAsync(
        ulong reporterUserId,
        MatchTeam winningTeam,
        CancellationToken cancellationToken = default
    );

    List<ActiveSessionSummary> GetActiveSessions();
    Task NotifyGameStartedAsync(string lobbyId);
    Task NotifyGameFinishedAsync(string lobbyId);
    Task DestroyLobbyAsync(string lobbyId);

    Task<QueueOperationResponse> LinkSteamIdAsync(
        ulong userId,
        ulong steamId,
        CancellationToken cancellationToken = default
    );

    Task<ChallengeOperationResponse> ChallengePlayerAsync(
        ulong challengerUserId,
        ulong challengedUserId,
        CancellationToken cancellationToken = default
    );
    Task<ChallengeOperationResponse> AcceptChallengeAsync(
        ulong userId,
        CancellationToken cancellationToken = default
    );
    Task<ChallengeOperationResponse> DeclineChallengeAsync(
        ulong userId,
        CancellationToken cancellationToken = default
    );

    Task<QueueOperationResponse> BanPlayerAsync(
        ulong userId,
        string? reason,
        CancellationToken cancellationToken = default
    );
    Task<QueueOperationResponse> UnbanPlayerAsync(
        ulong userId,
        CancellationToken cancellationToken = default
    );
    Task<QueueOperationResponse> KickPlayerAsync(ulong userId);
    Task<QueueOperationResponse> ForceStartLobbyAsync();
    Task<QueueOperationResponse> ResetQueueAsync();
    Task<QueueOperationResponse> AddSteamAccountAsync(string username, string password);
    PoolStatusResponse GetPoolStatus();
}
