namespace IHLDota2Bot.Discord.Configuration;

public class DiscordOptions
{
    public const string SectionName = "Discord";

    public string Token { get; set; } = string.Empty;
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Guild to register slash commands in. Guild commands propagate instantly;
    /// when 0 the commands are registered globally instead (Discord may take up
    /// to an hour to distribute those).
    /// </summary>
    public ulong GuildId { get; set; }

    public List<ulong> AdminUserIds { get; set; } = [];
    public string? AdminRoleName { get; set; }
}
