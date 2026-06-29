namespace IHLDota2Bot.Auth;

/// <summary>
/// Claim names carried by the player JWT. Inbound claim mapping is disabled on the
/// JwtBearer handler, so these stay exactly as written here on both sides.
/// </summary>
public static class PlayerClaims
{
    /// <summary>League user id. Equals the SteamID64 for Steam-registered players.</summary>
    public const string Subject = "sub";

    /// <summary>The authenticated SteamID64 (string).</summary>
    public const string SteamId = "steamId";

    /// <summary>Display name (omitted for not-yet-registered players).</summary>
    public const string Name = "name";

    /// <summary>"true" once the player has a league account; "false" pre-registration.</summary>
    public const string Registered = "registered";
}
