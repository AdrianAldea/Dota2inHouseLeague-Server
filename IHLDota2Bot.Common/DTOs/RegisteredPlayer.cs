namespace IHLDota2Bot.Common.DTOs;

public record RegisteredPlayer
{
    public required string SteamId { get; init; }
    public required string Username { get; init; }
    public required string UserId { get; init; }
}
