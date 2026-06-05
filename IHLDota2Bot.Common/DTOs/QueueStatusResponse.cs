namespace IHLDota2Bot.Common.DTOs;

public record QueueStatusResponse
{
    public required string? CurrentLobbyId { get; init; }
    public required int CurrentLobbyPlayerCount { get; init; }
    public required int PlayersPerLobby { get; init; }
    public required int PlayersReady { get; init; }
    public required int TotalRegistered { get; init; }
    public required int WaitingPlayers { get; init; }
    public required IReadOnlyList<QueuePlayerSummary> Players { get; init; }
}

public record QueuePlayerSummary
{
    public required string Username { get; init; }
    public required bool IsReady { get; init; }
}
