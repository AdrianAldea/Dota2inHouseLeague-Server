namespace IHLDota2Bot.Common.Requests;

/// <summary>
/// Body of <c>POST /api/auth/register</c>: the display name a freshly Steam-authenticated
/// player picks when creating their league account. SteamID64 comes from the JWT, not here.
/// </summary>
public record RegisterAccountRequest
{
    public required string Username { get; init; }
}
