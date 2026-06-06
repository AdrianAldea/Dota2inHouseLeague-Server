namespace IHLDota2Bot;

public class LobbyCreatedEventArgs : EventArgs
{
    public required Lobby Lobby { get; set; }
}

public class PlayerJoinedEventArgs : EventArgs
{
    public required Player Player { get; set; }
    public required Lobby Lobby { get; set; }
}

public class PlayerReadyEventArgs : EventArgs
{
    public required Player Player { get; set; }
}

public class LobbyFullEventArgs : EventArgs
{
    public required Lobby Lobby { get; set; }
}

public class LobbyReadyEventArgs : EventArgs
{
    public required Lobby Lobby { get; set; }
}
