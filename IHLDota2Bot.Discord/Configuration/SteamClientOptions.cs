namespace IHLDota2Bot.Discord.Configuration;

public class SteamClientOptions
{
    public const string SectionName = "SteamClient";

    // Must match the Game Manager's Aspire resource name in the AppHost
    // (Program.cs AddProject), or service discovery falls back to public DNS
    // and every call fails with "No such host is known".
    public string ServiceName { get; set; } = "Dota2inHouseLeague-Backend";
}
