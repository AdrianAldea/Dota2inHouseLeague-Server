namespace IHLDota2Bot.Configuration;

/// <summary>
/// Settings for the player-facing JWT issued after a successful Steam login. Bound
/// from the <c>PlayerJwt</c> configuration section. The signing key is a secret —
/// supply it via user-secrets or the <c>PlayerJwt__SigningKey</c> environment
/// variable. When the key is empty an ephemeral per-process key is generated and a
/// startup warning is logged, so development works without configuring secrets
/// (tokens simply don't survive a server restart).
/// </summary>
public class PlayerJwtOptions
{
    public const string SectionName = "PlayerJwt";

    public string Issuer { get; set; } = "ihl-dota";
    public string Audience { get; set; } = "ihl-dota-desktop";

    /// <summary>HMAC-SHA256 signing secret. Empty => ephemeral dev key (see class summary).</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Token lifetime. Defaults to 7 days so the desktop client rarely re-prompts.</summary>
    public int ExpiryMinutes { get; set; } = 60 * 24 * 7;
}
