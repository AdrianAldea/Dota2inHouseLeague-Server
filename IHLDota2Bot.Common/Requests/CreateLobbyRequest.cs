namespace IHLDota2Bot.Common.Requests;

public record CreateLobbyRequest
{
    public required string AuthorId { get; init; }
    public required string LobbyName { get; init; }
    public required string LobbyPassword { get; init; }

    // When non-empty, the SteamGcClient watches GC lobby snapshots and
    // auto-launches once every one of these SteamIDs is seated on a real
    // team slot (Radiant or Dire). Leave empty for the admin passthrough
    // path that just creates the lobby without auto-launch tracking.
    public IReadOnlyList<ulong> ExpectedSteamIds { get; init; } = [];

    // Opaque identifier the app uses to correlate this Dota lobby back to
    // its own match/lobby record. Echoed back via LobbyLaunchedEventArgs.
    public string? AppLobbyId { get; init; }
}
