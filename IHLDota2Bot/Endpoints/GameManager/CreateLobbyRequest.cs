namespace IHLDota2Bot.Endpoints.GameManager;

public class CreateLobbyRequest
{
    public required string LobbyName { get; set; }
    public required string LobbyPassword { get; set; }
}