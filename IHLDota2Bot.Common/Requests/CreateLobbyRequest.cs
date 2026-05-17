namespace IHLDota2Bot.Common.Requests;

public class CreateLobbyRequest
{
    public required string AuthorId { get; set; }
    public required string LobbyName { get; set; }
    public required string LobbyPassword { get; set; }
}