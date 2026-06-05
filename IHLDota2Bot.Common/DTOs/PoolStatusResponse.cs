namespace IHLDota2Bot.Common.DTOs;

public enum PoolAccountState
{
    Ready = 0,
    InUse = 1,
    Cooldown = 2
}

public record PoolStatusResponse
{
    public required int Total { get; init; }
    public required int Ready { get; init; }
    public required int InUse { get; init; }
    public required int OnCooldown { get; init; }
    public required IReadOnlyList<PoolAccountSummary> Accounts { get; init; }
}

public record PoolAccountSummary
{
    public required string Username { get; init; }
    public required PoolAccountState State { get; init; }
    public required string? CurrentLobbyId { get; init; }
    public required DateTime? LastUsedAt { get; init; }
}
