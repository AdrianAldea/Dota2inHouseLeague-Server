namespace IHLDota2Bot.Common.DTOs;

public enum PoolSessionState
{
    Ready = 0,
    InProgress = 1,
    Finished = 2
}

public record ActiveSessionSummary
{
    public required string LobbyId { get; init; }
    public required string AccountId { get; init; }
    public required int PlayerCount { get; init; }
    public required PoolSessionState State { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? GameStartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
}
