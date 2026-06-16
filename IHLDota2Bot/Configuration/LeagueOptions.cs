namespace IHLDota2Bot.Configuration;

/// <summary>
/// League sizing knobs that an operator may want to tune without a code change.
/// Bound from the <c>League</c> configuration section.
/// </summary>
public class LeagueOptions
{
    public const string SectionName = "League";

    /// <summary>
    /// Players required to fill a lobby before it locks for readying up. Defaults
    /// to <see cref="LeagueConstants.PlayersPerLobby"/> (a small value suited to
    /// testing); set <c>League:PlayersPerLobby</c> to 10 for a full 5v5 match.
    /// </summary>
    public int PlayersPerLobby { get; set; } = LeagueConstants.PlayersPerLobby;
}
