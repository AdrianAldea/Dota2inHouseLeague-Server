using IHLDota2Bot.Auth;

namespace IHLDota2Bot.Endpoints.GameManager;

/// <summary>
/// Dispatches /api/gamemanager subroutes to area-specific endpoint mappers.
/// Each mapper lives in its own file (PlayerEndpoints, QueueEndpoints, ...).
/// Admin endpoints are gated behind the <see cref="AdminApiKeyAuthenticationHandler"/>
/// scheme; an <c>X-Api-Key</c> header with the configured secret is required.
/// </summary>
public static class GameManagerEndpoints
{
    public static RouteGroupBuilder MapGameManagerEndpoints(this RouteGroupBuilder group)
    {
        group.MapPlayerEndpoints();
        group.MapQueueEndpoints();
        group.MapLobbyEndpoints();
        group.MapMatchEndpoints();
        group.MapChallengeEndpoints();

        // Admin routes require authentication. When AdminApiKey is non-empty in
        // config, only callers presenting the correct X-Api-Key header are allowed;
        // when the key is unconfigured the handler succeeds unconditionally (dev mode).
        group.MapGroup(string.Empty)
            .RequireAuthorization()
            .MapAdminEndpoints();

        return group;
    }
}
