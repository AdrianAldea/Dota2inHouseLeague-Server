using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Mvc;

namespace IHLDota2Bot.Endpoints.GameManager;

/// <summary>
/// Lobby management endpoints (create / invite).
/// </summary>
public static class LobbyEndpoints
{
    public static RouteGroupBuilder MapLobbyEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost(
            "/create",
            async (
                IDota2Service svc,
                ILogger<Dota2Service> log,
                [FromBody] CreateLobbyRequest req
            ) =>
            {
                try
                {
                    await svc.CreateLobbyAsync(req);
                    return TypedResults.Ok();
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Error creating lobby");
                    return Results.StatusCode(500);
                }
            }
        );

        group.MapPost(
            "/invite",
            (IDota2Service svc, ILogger<Dota2Service> log, [FromBody] InviteRequest req) =>
                EndpointHelpers.Try(
                    log,
                    "invite players",
                    () => svc.InvitePlayersToLobby(req.PlayersIds)
                )
        );

        return group;
    }
}
