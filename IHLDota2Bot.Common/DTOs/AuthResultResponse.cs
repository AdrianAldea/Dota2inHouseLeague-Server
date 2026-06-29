namespace IHLDota2Bot.Common.DTOs;

/// <summary>
/// Returned by <c>POST /api/auth/register</c>: a fresh registered-player JWT plus the
/// new profile, so the desktop client can swap its token and land straight on the profile.
/// </summary>
public record AuthResultResponse
{
    public required string Token { get; init; }
    public required bool Registered { get; init; }
    public PlayerStatsResponse? Profile { get; init; }
}
