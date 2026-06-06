namespace IHLDota2Bot;

public class GameReadyEventArgs : EventArgs
{
    public required Lobby Lobby { get; set; }
    public required SteamAccount Account { get; set; }
    public required LobbySession Session { get; set; }
}

public class GameStartedEventArgs : EventArgs
{
    public required LobbySession Session { get; set; }
}

public class GameFinishedEventArgs : EventArgs
{
    public required LobbySession Session { get; set; }
}
