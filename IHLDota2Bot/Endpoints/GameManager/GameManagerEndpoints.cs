
using Dota2Bot.Endpoints.GameManager;
using Dota2Bot.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Dota2Bot.Endpoints.StartUp;

public static class GameManagerEndpoints
{
    public static void MapGameManagerEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/create", CreateLobby);
        group.MapPost("/invite", InvitePlayersToLobby);
    }

    private static Results<Ok<bool>, StatusCodeHttpResult> CreateLobby(
        SteamService steamService,
        [FromBody] CreateLobbyRequest request,
        CancellationToken cancellationToken
    )
    {
        steamService.CreateLobby(new CreateLobbyRequest { LobbyName = "GameRomania1", LobbyPassword = "GameRomania1" });

        return TypedResults.Ok(true);
    }

    private static Results<Ok<bool>, StatusCodeHttpResult> InvitePlayersToLobby(
        SteamService steamService,
        [FromBody] InviteRequest request,
        CancellationToken cancellationToken
    )
    {
        steamService.InvitePlayersToLobby([76561198046987929]);

        return TypedResults.Ok(true);
    }
}
