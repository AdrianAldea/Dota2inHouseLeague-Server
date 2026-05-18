namespace IHLDota2Bot.Common.Requests;

public record RegisterPlayerRequest
{
    public required ulong SteamId { get; init; }
    public required ulong UserId { get; init; }
    public required string Username { get; init; }
}
