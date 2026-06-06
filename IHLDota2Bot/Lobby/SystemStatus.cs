namespace IHLDota2Bot;

public class SystemStatus
{
    public AccountPoolStats PoolStats { get; set; } = new();
    public QueueStats QueueStats { get; set; } = new();
    public List<LobbySession> ActiveSessions { get; set; } = [];
    public DateTime Timestamp { get; set; }
}
