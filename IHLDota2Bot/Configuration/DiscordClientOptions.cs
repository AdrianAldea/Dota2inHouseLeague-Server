namespace IHLDota2Bot.Configuration;

public class DiscordClientOptions
{
    public const string SectionName = "DiscordClient";

    // Must match the Discord service's Aspire resource name in the AppHost
    // (Program.cs AddProject), or service discovery falls back to public DNS
    // and every call fails with "No such host is known".
    public string ServiceName { get; set; } = "Dota2inHouseLeague-Backend-Discord";
}
