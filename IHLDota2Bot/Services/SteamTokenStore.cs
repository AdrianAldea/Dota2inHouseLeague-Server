using System.Text.Json;
using System.Text.Json.Nodes;

namespace IHLDota2Bot.Services;

/// <summary>
/// Persists Steam <em>refresh tokens</em> per account inside <c>appsettings.json</c>,
/// under <c>Steam:RefreshTokens:&lt;account&gt;</c>, so the bot logs on with a stored
/// token instead of re-submitting the password on every start.
///
/// SteamKit2 3.x logs on through an authentication session that exchanges the
/// username/password (plus any SteamGuard challenge) for a long-lived refresh token.
/// Re-running that credential exchange on each connect is what trips Steam's login
/// throttle; reusing a stored token avoids it. Keeping the token in appsettings (rather
/// than a per-machine file under %LOCALAPPDATA%) means it travels with the configuration.
///
/// Reads and writes go straight to the JSON file via <see cref="JsonNode"/> so the
/// rest of the file is preserved; the in-memory <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>
/// snapshot isn't consulted (it can be stale after a write and is only re-read on the
/// next start, which is exactly when a persisted token matters).
/// </summary>
public sealed class SteamTokenStore
{
    private const string SteamSection = "Steam";
    private const string TokensSection = "RefreshTokens";

    private readonly string appSettingsPath;
    private readonly Lock gate = new();

    public SteamTokenStore(string? appSettingsPath = null)
    {
        // Default to the appsettings.json next to the running app. Callers (the pool)
        // pass the host content-root path so writes land on the project's appsettings.json.
        this.appSettingsPath = appSettingsPath
            ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    /// <summary>Returns the stored refresh token for the account, or null if none is saved.</summary>
    public string? TryLoad(string username)
    {
        lock (gate)
        {
            var root = ReadRoot();
            var token = (root[SteamSection] as JsonObject)?[TokensSection] is JsonObject tokens
                ? tokens[username]?.GetValue<string>()
                : null;
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
    }

    /// <summary>Saves (or overwrites) the refresh token for the account in appsettings.json.</summary>
    public void Save(string username, string refreshToken)
    {
        lock (gate)
        {
            var root = ReadRoot();
            var tokens = GetOrCreateTokensObject(root);
            tokens[username] = refreshToken;
            WriteRoot(root);
        }
    }

    /// <summary>Removes any stored token for the account (e.g. after it's rejected as expired).</summary>
    public void Clear(string username)
    {
        lock (gate)
        {
            var root = ReadRoot();
            if ((root[SteamSection] as JsonObject)?[TokensSection] is JsonObject tokens
                && tokens.Remove(username))
            {
                WriteRoot(root);
            }
        }
    }

    private JsonObject ReadRoot()
    {
        if (!File.Exists(appSettingsPath)) return new JsonObject();
        var text = File.ReadAllText(appSettingsPath);
        return string.IsNullOrWhiteSpace(text)
            ? new JsonObject()
            : JsonNode.Parse(text) as JsonObject ?? new JsonObject();
    }

    private void WriteRoot(JsonObject root)
    {
        var text = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(appSettingsPath, text);
    }

    private static JsonObject GetOrCreateTokensObject(JsonObject root)
    {
        if (root[SteamSection] is not JsonObject steam)
        {
            steam = new JsonObject();
            root[SteamSection] = steam;
        }
        if (steam[TokensSection] is not JsonObject tokens)
        {
            tokens = new JsonObject();
            steam[TokensSection] = tokens;
        }
        return tokens;
    }
}
