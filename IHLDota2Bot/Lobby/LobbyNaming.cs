namespace IHLDota2Bot;

/// <summary>
/// Derives the Dota practice-lobby name/password from the app lobby id. The
/// derivation is deterministic so both the launch path (<see cref="LobbyPoolManager"/>)
/// and the player-facing <c>/me/match</c> endpoint agree on the credentials without
/// having to persist them.
/// </summary>
public static class LobbyNaming
{
    /// <summary>Lobby name and password share the same value.</summary>
    public static string For(string lobbyId)
    {
        ArgumentNullException.ThrowIfNull(lobbyId);
        var shortId = lobbyId.Length >= 8 ? lobbyId[..8] : lobbyId;
        return $"Dota2IHL-{shortId}";
    }
}
