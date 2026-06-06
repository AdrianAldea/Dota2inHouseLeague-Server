using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Data.Entities;

namespace IHLDota2Bot.Data.Repositories;

public interface IMatchRepository
{
    Task<MatchEntity> CreateMatchAsync(string? lobbyId, IEnumerable<(ulong UserId, MatchTeam Team)> players, CancellationToken cancellationToken = default);
    Task<MatchSummary?> GetCurrentMatchForPlayerAsync(ulong userId, CancellationToken cancellationToken = default);
    Task<MatchSummary?> GetByIdAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<RecentMatchesResponse> GetRecentMatchesForPlayerAsync(ulong userId, int top, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a match completed, set the winner, and apply rating/wins/losses to participants.
    /// Returns null if the match isn't found, or the (possibly already-completed) match summary otherwise.
    /// </summary>
    Task<ReportMatchResult> ReportMatchAsync(ulong reporterUserId, MatchTeam winningTeam, CancellationToken cancellationToken = default);
}

public record ReportMatchResult
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public MatchSummary? Match { get; init; }
}
