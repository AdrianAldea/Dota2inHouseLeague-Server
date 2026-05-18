namespace IHLDota2Bot.Common.Requests;

public record CreateLobbyRequest
{
    public required string AuthorId { get; init; }
    public required string LobbyName { get; init; }
    public required string LobbyPassword { get; init; }
}
