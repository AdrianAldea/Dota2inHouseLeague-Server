namespace IHLDota2Bot;

/// <summary>
/// In-memory representation of a player participating in queue/lobby flow.
/// Persistence lives in <see cref="Data.Entities.PlayerEntity"/>.
/// </summary>
public class Player
{
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string SteamId { get; set; } = string.Empty;
    public string? CurrentLobbyId { get; set; }
    public bool IsReady { get; set; }
    public DateTime RegisteredAt { get; set; }

    public override string ToString() =>
        $"{Username} ({SteamId}) - Ready: {IsReady} - Lobby: {CurrentLobbyId ?? "None"}";
}
