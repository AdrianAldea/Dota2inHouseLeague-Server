namespace IHLDota2Bot.Common.Requests;

public record RegisterPlayerRequest
{
    public ulong SteamId { get; set; }
    public ulong UserId { get; set; }
    public string Username { get; set; }
}