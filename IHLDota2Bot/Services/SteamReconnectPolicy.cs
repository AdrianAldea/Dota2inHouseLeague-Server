using SteamKit2;

namespace IHLDota2Bot.Services;

/// <summary>
/// Pure reconnection policy for <see cref="SteamGcClient"/>: how long to wait
/// before the next reconnect attempt, and whether a logon failure is worth
/// retrying at all. Kept separate from the SteamKit-bound client so the policy
/// can be unit-tested in isolation.
/// </summary>
public static class SteamReconnectPolicy
{
    /// <summary>First reconnect delay; doubles on each subsequent attempt.</summary>
    public static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(5);

    /// <summary>Upper bound on the reconnect delay.</summary>
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Exponential backoff for a zero-based <paramref name="attempt"/> number,
    /// capped at <see cref="MaxDelay"/>: attempt 0 → 5s, 1 → 10s, 2 → 20s, … .
    /// A prolonged Steam outage settles at the cap rather than hammering the CMs,
    /// while a brief blip still recovers quickly. Negative inputs are treated as 0.
    /// </summary>
    public static TimeSpan NextDelay(int attempt)
    {
        if (attempt < 0)
            attempt = 0;
        var seconds = Math.Min(
            BaseDelay.TotalSeconds * Math.Pow(2, attempt),
            MaxDelay.TotalSeconds
        );
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// True when Steam rejected the logon for a reason that retrying the same
    /// credentials can't fix (bad password, SteamGuard required, account
    /// disabled/banned). The reconnect loop should stop in those cases rather
    /// than re-submitting credentials that can't succeed and risking a lockout.
    /// </summary>
    public static bool IsNonRetryableLogonResult(EResult result) =>
        result switch
        {
            EResult.InvalidPassword => true,
            EResult.AccountLogonDenied => true, // SteamGuard email code required
            EResult.AccountLoginDeniedNeedTwoFactor => true,
            EResult.AccountDisabled => true,
            EResult.Banned => true,
            _ => false,
        };

    /// <summary>
    /// True when a logon performed with a persisted refresh token failed because the
    /// token itself is no longer good (expired or revoked). The client should discard
    /// the stored token and fall back to a fresh credential auth on the next connect,
    /// rather than treating it as a permanent credential failure.
    /// </summary>
    public static bool IsStaleTokenResult(EResult result) =>
        result switch
        {
            EResult.Expired => true,
            EResult.AccessDenied => true,
            EResult.InvalidPassword => true, // Steam reports a dead access token this way too
            _ => false,
        };
}
