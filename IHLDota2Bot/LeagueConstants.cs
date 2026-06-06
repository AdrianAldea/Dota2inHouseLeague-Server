namespace IHLDota2Bot;

/// <summary>
/// Domain constants for the in-house league. Centralising these keeps
/// magic numbers out of business logic and makes future tuning a one-line change.
/// </summary>
public static class LeagueConstants
{
    /// <summary>Players required to fill a full lobby (5v5 Dota match).</summary>
    public const int PlayersPerLobby = 10;

    /// <summary>Default rating assigned to newly registered players.</summary>
    public const int DefaultRating = 1000;

    /// <summary>ELO K-factor applied per completed match.</summary>
    public const int EloKFactor = 32;

    /// <summary>How long a pending challenge stays valid before expiring.</summary>
    public static readonly TimeSpan ChallengeTtl = TimeSpan.FromMinutes(15);

    /// <summary>Upper bound on `!leaderboard N`.</summary>
    public const int MaxLeaderboardTop = 50;

    /// <summary>Upper bound on `!matches N`.</summary>
    public const int MaxRecentMatches = 20;
}
