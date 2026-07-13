namespace IHLDota2Bot;

public class QueueStats
{
    public int TotalRegisteredPlayers { get; set; }
    public Lobby? CurrentLobby { get; set; }
    public int PlayersPerLobby { get; set; }
    public int CurrentLobbyPlayerCount { get; set; }
    public int PlayersReady { get; set; }
    public int WaitingPlayers { get; set; }
    public int TotalLobbiesCreated { get; set; }
    public int FinishedLobbies { get; set; }

    public override string ToString() =>
        $"Queue: Registered={TotalRegisteredPlayers}, Waiting={WaitingPlayers}, "
        + $"Current Lobby={CurrentLobbyPlayerCount}/{PlayersPerLobby} (Ready: {PlayersReady})";
}
