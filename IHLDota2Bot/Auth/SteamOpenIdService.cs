using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;

namespace IHLDota2Bot.Auth;

/// <summary>
/// Minimal Steam "Sign in through Steam" (OpenID 2.0) support — no cookie middleware,
/// so it stays API/token-friendly. Builds the provider redirect, then verifies the
/// signed assertion by echoing it back to Steam with <c>mode=check_authentication</c>
/// and extracting the SteamID64 from the claimed identifier.
/// </summary>
public sealed partial class SteamOpenIdService(HttpClient http)
{
    public const string SteamLoginUrl = "https://steamcommunity.com/openid/login";

    /// <summary>Builds the URL to send the user's browser to for Steam authentication.</summary>
    public string BuildRedirectUrl(string realm, string returnTo)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["openid.ns"] = "http://specs.openid.net/auth/2.0",
            ["openid.mode"] = "checkid_setup",
            ["openid.return_to"] = returnTo,
            ["openid.realm"] = realm,
            ["openid.identity"] = "http://specs.openid.net/auth/2.0/identifier_select",
            ["openid.claimed_id"] = "http://specs.openid.net/auth/2.0/identifier_select",
        };
        return QueryHelpers.AddQueryString(SteamLoginUrl, parameters);
    }

    /// <summary>
    /// Verifies the OpenID assertion in the callback query. Returns the SteamID64 on
    /// success, or <c>null</c> if Steam rejects the signature or the id can't be parsed.
    /// </summary>
    public async Task<string?> VerifyAsync(
        IEnumerable<KeyValuePair<string, string>> query,
        CancellationToken cancellationToken = default
    )
    {
        string? claimedId = null;
        var form = new List<KeyValuePair<string, string>>();
        foreach (var pair in query)
        {
            if (!pair.Key.StartsWith("openid.", StringComparison.Ordinal))
                continue;
            if (pair.Key == "openid.claimed_id")
                claimedId = pair.Value;
            // Echo every openid.* field back except mode, which we replace below.
            if (pair.Key != "openid.mode")
                form.Add(pair);
        }
        form.Add(new KeyValuePair<string, string>("openid.mode", "check_authentication"));

        using var content = new FormUrlEncodedContent(form);
        using var response = await http.PostAsync(SteamLoginUrl, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (
            !response.IsSuccessStatusCode
            || !body.Contains("is_valid:true", StringComparison.Ordinal)
        )
            return null;

        if (claimedId is null)
            return null;

        var match = SteamIdRegex().Match(claimedId);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"^https?://steamcommunity\.com/openid/id/(\d+)/?$", RegexOptions.IgnoreCase)]
    private static partial Regex SteamIdRegex();
}
