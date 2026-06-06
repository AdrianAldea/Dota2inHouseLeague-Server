namespace IHLDota2Bot;

public class Lobby
{
    public string Id { get; set; } = string.Empty;
    public List<Player> Players { get; set; } = [];
    public LobbyState State { get; set; }
    public string? SteamAccountId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public int ReadyCount => Players.Count(p => p.IsReady);
    public int TotalCount => Players.Count;

    public override string ToString() =>
        $"Lobby: {Id} | State: {State} | Players: {ReadyCount}/{TotalCount} Ready | Account: {SteamAccountId ?? "None"}";
}

public enum LobbyState
{
    Waiting,
    Ready,
    InProgress,
    Finished
}
