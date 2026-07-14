namespace IHLDota2Bot.Configuration;

/// <summary>
/// Fixed-window per-IP rate limit for the player-facing surface (<c>/api/auth/*</c>
/// and the JWT me-endpoints). Bound from the <c>PlayerRateLimit</c> configuration
/// section. Disabled by default (<see cref="PermitLimit"/> = 0) so development and
/// tests are unaffected; set a positive limit in production, e.g. 60 requests per
/// 60-second window.
/// </summary>
public class PlayerRateLimitOptions
{
    public const string SectionName = "PlayerRateLimit";

    /// <summary>Requests allowed per window per client IP. 0 (default) disables limiting.</summary>
    public int PermitLimit { get; set; }

    /// <summary>Window length in seconds. Defaults to 60.</summary>
    public int WindowSeconds { get; set; } = 60;
}
