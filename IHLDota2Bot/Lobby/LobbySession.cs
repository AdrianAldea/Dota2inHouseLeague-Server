namespace IHLDota2Bot;

public class LobbySession
{
    public string LobbyId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public List<Player> Players { get; set; } = [];
    public LobbySessionState State { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? GameStartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public override string ToString() =>
        $"Session: {LobbyId} | Account: {AccountId} | State: {State} | Players: {Players.Count}";
}

public enum LobbySessionState
{
    Ready,
    InProgress,
    Finished
}
