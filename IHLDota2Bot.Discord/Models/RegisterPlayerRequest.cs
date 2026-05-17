namespace IHLDota2Bot.Discord.Models;

public record RegisterPlayerRequest
{
    public ulong SteamId { get; set; }
    public ulong UserId { get; set; }
}