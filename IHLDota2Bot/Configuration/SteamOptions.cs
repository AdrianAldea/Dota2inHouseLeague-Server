namespace IHLDota2Bot.Configuration;

public class SteamOptions
{
    public const string SectionName = "Steam";

    public SteamAccountCredentials BotAccount { get; set; } = new();
    public List<SteamAccountCredentials> PoolAccounts { get; set; } = [];
}

public class SteamAccountCredentials
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
