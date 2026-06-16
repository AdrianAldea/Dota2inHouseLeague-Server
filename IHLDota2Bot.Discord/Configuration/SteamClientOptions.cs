namespace IHLDota2Bot.Discord.Configuration;

public class SteamClientOptions
{
    public const string SectionName = "SteamClient";

    // Must match the Game Manager's Aspire resource name in the AppHost
    // (Program.cs AddProject), or service discovery falls back to public DNS
    // and every call fails with "No such host is known".
    public string ServiceName { get; set; } = "Dota2inHouseLeague-Backend";

    /// <summary>
    /// Sent as <c>X-Api-Key</c> on every Game Manager HTTP request. Must match
    /// <c>GameManager:AdminApiKey</c> on the backend. Supply via user-secrets or
    /// <c>SteamClient__ApiKey</c> environment variable; leave empty in development
    /// when the backend gate is also unconfigured.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
