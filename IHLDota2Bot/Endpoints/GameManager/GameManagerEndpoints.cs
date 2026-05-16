using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace IHLDota2Bot.Endpoints.GameManager;

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
        try
        {
            steamService.CreateLobby(new CreateLobbyRequest
            {
                LobbyName = request.LobbyName,
                LobbyPassword = request.LobbyPassword
            });

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            // Log the exception (if logging is configured)
            Console.Error.WriteLine($"Error creating lobby: {ex.Message}");
            return TypedResults.StatusCode(500);
        }
    }

    private static Results<Ok<bool>, StatusCodeHttpResult> InvitePlayersToLobby(
        SteamService steamService,
        [FromBody] InviteRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            steamService.InvitePlayersToLobby(request.PlayersIds);

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            // Log the exception (if logging is configured)
            Console.Error.WriteLine($"Error inviting players to lobby: {ex.Message}");
            return TypedResults.StatusCode(500);
        }
    }
}
