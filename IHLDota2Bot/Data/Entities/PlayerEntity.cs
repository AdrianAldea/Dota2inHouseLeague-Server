namespace IHLDota2Bot.Data.Entities;

public class PlayerEntity
{
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string SteamId { get; set; } = string.Empty;
    public int Rating { get; set; } = LeagueConstants.DefaultRating;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }
    public DateTime? BannedAt { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
}
