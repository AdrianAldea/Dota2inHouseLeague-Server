namespace IHLDota2Bot.Common.DTOs;

/// <summary>
/// Returned by <c>GET /api/auth/me</c>: the identity behind the current JWT.
/// <see cref="Registered"/> is false (and <see cref="Profile"/> null) when the Steam
/// account hasn't completed league registration yet.
/// </summary>
public record MeResponse
{
    public required ulong UserId { get; init; }
    public required string SteamId { get; init; }
    public required bool Registered { get; init; }
    public PlayerStatsResponse? Profile { get; init; }
}
