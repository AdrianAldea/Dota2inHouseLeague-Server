namespace IHLDota2Bot.Common.DTOs;

/// <summary>
/// The signed-in player's current in-progress match, as served by
/// <c>GET /api/gamemanager/me/match</c>: the match summary (teams, sides, state)
/// plus the Dota practice-lobby credentials so a player who missed the Steam
/// invite can join through the in-game lobby browser. Credentials are only
/// exposed to the match's own participants (identity comes from the JWT).
/// </summary>
public record CurrentMatchResponse
{
    public required MatchSummary Match { get; init; }

    /// <summary>Dota lobby name, or null when no lobby is attached to the match.</summary>
    public required string? LobbyName { get; init; }

    /// <summary>Dota lobby password, or null when no lobby is attached to the match.</summary>
    public required string? LobbyPassword { get; init; }
}
