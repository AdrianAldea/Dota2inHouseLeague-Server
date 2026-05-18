namespace IHLDota2Bot.Data.Entities;

public class PlayerEntity
{
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string SteamId { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
}
