namespace IHLDota2Bot.Common.DTOs;

public enum ChallengeStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Expired = 3,
    Cancelled = 4,
}

public record ChallengeResponse
{
    public required Guid Id { get; init; }
    public required ulong ChallengerUserId { get; init; }
    public required string ChallengerUsername { get; init; }
    public required ulong ChallengedUserId { get; init; }
    public required string ChallengedUsername { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required ChallengeStatus Status { get; init; }
    public Guid? ResultingMatchId { get; init; }
}

public record ChallengeOperationResponse
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public ChallengeResponse? Challenge { get; init; }
    public MatchSummary? Match { get; init; }
}
