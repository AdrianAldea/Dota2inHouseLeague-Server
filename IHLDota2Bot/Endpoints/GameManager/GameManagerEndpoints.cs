namespace IHLDota2Bot.Endpoints.GameManager;

/// <summary>
/// Dispatches /api/gamemanager subroutes to area-specific endpoint mappers.
/// Each mapper lives in its own file (PlayerEndpoints, QueueEndpoints, ...).
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
        group.MapAdminEndpoints();
        return group;
    }
}
