namespace IHLDota2Bot.Common.DTOs;

public record MatchSummary
{
    public required Guid Id { get; init; }
    public required string? LobbyId { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime? CompletedAt { get; init; }
    public required MatchStatus Status { get; init; }
    public required MatchTeam? WinningTeam { get; init; }
    public required IReadOnlyList<MatchPlayerSummary> Players { get; init; }
}

public record MatchPlayerSummary
{
    public required ulong UserId { get; init; }
    public required string Username { get; init; }
    public required MatchTeam Team { get; init; }
    public required bool Won { get; init; }
}

public record RecentMatchesResponse
{
    public required ulong UserId { get; init; }
    public required int Count { get; init; }
    public required IReadOnlyList<MatchSummary> Matches { get; init; }
}
