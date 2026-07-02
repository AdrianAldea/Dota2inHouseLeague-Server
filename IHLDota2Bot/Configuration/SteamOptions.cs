namespace IHLDota2Bot.Configuration;

public class SteamOptions
{
    public const string SectionName = "Steam";

    public SteamAccountCredentials BotAccount { get; set; } = new();
    public List<SteamAccountCredentials> PoolAccounts { get; set; } = [];

    /// <summary>
    /// When true (default), a dropped Steam connection is retried with exponential
    /// backoff. Set to false to disable the reconnect loop entirely, so a single
    /// disconnect/logoff leaves the client down until the process is restarted —
    /// useful when diagnosing logon failures without the client hammering the CMs.
    /// </summary>
    public bool EnableReconnect { get; set; } = true;
}

public class SteamAccountCredentials
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
