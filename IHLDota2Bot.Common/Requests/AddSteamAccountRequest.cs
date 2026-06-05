namespace IHLDota2Bot.Common.Requests;

public record AddSteamAccountRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}
