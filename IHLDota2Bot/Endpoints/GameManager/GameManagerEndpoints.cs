using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;

namespace IHLDota2Bot.Endpoints.GameManager;

public static class GameManagerEndpoints
{
    public static void MapGameManagerEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/create", CreateLobby);
        group.MapPost("/register", RegisterPlayer);
        group.MapPost("/profile", CreateLobby);
        group.MapPost("/invite", InvitePlayersToLobby);
    }

    private static async Task<Results<Ok<RegisteredPlayer>, StatusCodeHttpResult>> RegisterPlayer(
        GameManagerService service,
        [FromBody] RegisterPlayerRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.RegisterPlayer(request);

            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            // Log the exception (if logging is configured)
            Console.Error.WriteLine($"Error creating lobby: {ex.Message}");
            return TypedResults.StatusCode(500);
        }
    }

    private static Results<Ok<bool>, StatusCodeHttpResult> CreateLobby(
        GameManagerService service,
        [FromBody] CreateLobbyRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            service.CreateLobby(new CreateLobbyRequest
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
        GameManagerService service,
        [FromBody] InviteRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            service.InvitePlayersToLobby(request.PlayersIds);

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
