namespace IHLDota2Bot.Discord.Configuration;

public class DiscordOptions
{
    public const string SectionName = "Discord";

    public string Token { get; set; } = string.Empty;
    public ulong ChannelId { get; set; }
    public List<ulong> AdminUserIds { get; set; } = [];
    public string? AdminRoleName { get; set; }
}
