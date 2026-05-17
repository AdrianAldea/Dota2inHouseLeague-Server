namespace IHLDota2Bot.Discord.Models;

public class ProfileResponse
{
    public string Username { get; set; }
    public string SteamId { get; set; }
    public int Rating { get; set; }
    public int Matches { get; set; }
    public int Wins { get; set; }
}