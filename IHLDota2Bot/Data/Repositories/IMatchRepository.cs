using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Data.Entities;

namespace IHLDota2Bot.Data.Repositories;

public interface IMatchRepository
{
    Task<MatchEntity> CreateMatchAsync(string? lobbyId, IEnumerable<(ulong UserId, MatchTeam Team)> players, CancellationToken cancellationToken = default);
    Task<MatchSummary?> GetCurrentMatchForPlayerAsync(ulong userId, CancellationToken cancellationToken = default);
    Task<MatchSummary?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only lookup of the most recent match created for <paramref name="lobbyId"/>.
    /// Returns null when the lobby never produced a match (or no longer has one).
    /// Lets the admin panel jump straight from an active lobby to its match details
    /// without first discovering the match GUID.
    /// </summary>
    Task<MatchSummary?> GetByLobbyIdAsync(string lobbyId, CancellationToken cancellationToken = default);
    Task<RecentMatchesResponse> GetRecentMatchesForPlayerAsync(ulong userId, int top, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a match completed, set the winner, and apply rating/wins/losses to participants.
    /// Returns null if the match isn't found, or the (possibly already-completed) match summary otherwise.
    /// </summary>
    Task<ReportMatchResult> ReportMatchAsync(ulong reporterUserId, MatchTeam winningTeam, CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles the InProgress match for <paramref name="lobbyId"/> using a result
    /// the Dota GC reported (rather than a player !report). When
    /// <paramref name="winningTeam"/> has a value the match is Completed and
    /// rating/W-L is applied; when it's null (the GC didn't score the game —
    /// abandon, crash, never started) the match is Cancelled with no rating
    /// change. Idempotent: a no-op returning the existing summary if the match
    /// is already settled or no InProgress match exists for the lobby.
    /// </summary>
    Task<ReportMatchResult> CompleteMatchByLobbyAsync(string lobbyId, MatchTeam? winningTeam, CancellationToken cancellationToken = default);

    /// <summary>
    /// All matches still marked InProgress whose CreatedAt is older than <paramref name="olderThanUtc"/>.
    /// Used by the startup reconciliation pass in <see cref="LobbyPoolManager"/> to clean up matches
    /// stranded by a crash / kill mid-game (the in-memory <c>LobbySession</c> is gone but the DB row
    /// would otherwise stay InProgress forever).
    /// </summary>
    Task<List<MatchEntity>> GetStaleInProgressMatchesAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a match as Cancelled. Does not touch player Wins/Losses/Rating —
    /// a cancelled match never produced a winner so there's nothing to apply.
    /// Idempotent: calling on an already non-InProgress match is a no-op.
    /// </summary>
    Task<bool> CancelMatchAsync(Guid matchId, CancellationToken cancellationToken = default);
}

public record ReportMatchResult
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public MatchSummary? Match { get; init; }
}
