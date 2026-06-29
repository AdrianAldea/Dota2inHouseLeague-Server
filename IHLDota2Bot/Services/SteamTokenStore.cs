namespace IHLDota2Bot.Services;

/// <summary>
/// Persists Steam <em>refresh tokens</em> per account on disk so the bot can log on
/// with a stored token instead of re-submitting the password on every start.
///
/// SteamKit2 3.x logs on through an authentication session that exchanges the
/// username/password (plus any SteamGuard challenge) for a long-lived refresh token.
/// Re-running that credential exchange on each connect is exactly what trips Steam's
/// login throttle; reusing a stored token avoids it. Tokens land under
/// <c>%LOCALAPPDATA%/IHLDota2Bot/steam-tokens/&lt;account&gt;.token</c> by default.
/// </summary>
public sealed class SteamTokenStore
{
    private readonly string directory;
    private readonly Lock gate = new();

    public SteamTokenStore(string? directory = null)
    {
        this.directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IHLDota2Bot", "steam-tokens");
    }

    /// <summary>Returns the stored refresh token for the account, or null if none is saved.</summary>
    public string? TryLoad(string username)
    {
        var path = PathFor(username);
        lock (gate)
        {
            if (!File.Exists(path)) return null;
            var token = File.ReadAllText(path).Trim();
            return string.IsNullOrEmpty(token) ? null : token;
        }
    }

    /// <summary>Saves (or overwrites) the refresh token for the account.</summary>
    public void Save(string username, string refreshToken)
    {
        lock (gate)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(PathFor(username), refreshToken);
        }
    }

    /// <summary>Removes any stored token for the account (e.g. after it's rejected as expired).</summary>
    public void Clear(string username)
    {
        lock (gate)
        {
            var path = PathFor(username);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private string PathFor(string username)
    {
        // Steam account names are alphanumeric/underscore, but sanitise defensively so a
        // stray character can't escape the tokens directory or produce an invalid path.
        var safe = string.Concat(username.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        return Path.Combine(directory, safe + ".token");
    }
}
