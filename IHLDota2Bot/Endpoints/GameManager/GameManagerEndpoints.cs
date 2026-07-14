using IHLDota2Bot.Auth;

namespace IHLDota2Bot.Endpoints.GameManager;

/// <summary>
/// Dispatches /api/gamemanager subroutes to area-specific endpoint mappers.
/// Each mapper lives in its own file (PlayerEndpoints, QueueEndpoints, ...).
///
/// Three trust tiers share this group:
/// <list type="bullet">
///   <item><b>Public reads</b> (queue status, leaderboard, stats, match reads) —
///     no auth; they expose nothing a league member shouldn't see.</item>
///   <item><b>Player actions</b> — the JWT-protected me-endpoints; identity comes
///     from the token, so a player can only act as themselves.</item>
///   <item><b>Service routes</b> — every id-based mutation plus the admin routes.
///     These act on arbitrary players by id, so they are reserved for the trusted
///     services that already authenticate with the <c>X-Api-Key</c> header (the
///     Discord bot and the admin panel) via the
///     <see cref="AdminApiKeyAuthenticationHandler"/> scheme. When AdminApiKey is
///     unconfigured the handler succeeds unconditionally (dev mode).</item>
/// </list>
/// </summary>
public static class GameManagerEndpoints
{
    public static RouteGroupBuilder MapGameManagerEndpoints(this RouteGroupBuilder group)
    {
        // Id-based mutations act on any player by id — service/admin callers only.
        var service = group.MapGroup(string.Empty).RequireAuthorization();

        group.MapPlayerEndpoints(service);
        group.MapQueueEndpoints(service);
        service.MapLobbyEndpoints();
        group.MapMatchEndpoints(service);
        service.MapChallengeEndpoints();
        group.MapMeEndpoints();
        service.MapAdminEndpoints();

        return group;
    }
}
