using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace IHLDota2Bot.Endpoints.GameManager;

public static class GameManagerEndpoints
{
    public static void MapGameManagerEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/create", CreateLobby);
        group.MapGet("/player/{id}", GetPlayerProfile);
        group.MapPost("/register", RegisterPlayer);
        group.MapPost("/ready/{id}", MarkPlayerAsReady);
        group.MapPost("/invite", InvitePlayersToLobby);
    }

    private static async Task<Results<Ok, StatusCodeHttpResult>> MarkPlayerAsReady(
        Services.Dota2Service service,
        [FromRoute] ulong id,
        CancellationToken cancellationToken
    )
    {
            try
            {
                await service.MarkPlayerAsReady(id);

                return TypedResults.Ok();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error marking player as ready: {ex.Message}");
                return TypedResults.StatusCode(500);
            }
        }

    private static async Task<Results<Ok<PlayerProfile>, NotFound, StatusCodeHttpResult>> GetPlayerProfile(
        Services.Dota2Service service,
        [FromRoute] ulong id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.GetPlayerProfile(id);

            if (result == null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error retriving player profile: {ex.Message}");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<RegisteredPlayer>, StatusCodeHttpResult>> RegisterPlayer(
        Services.Dota2Service service,
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
            Console.Error.WriteLine($"Error registering player: {ex.Message}");
            return TypedResults.StatusCode(500);
        }
    }

    private static Results<Ok, StatusCodeHttpResult> CreateLobby(
        Services.Dota2Service service,
        [FromBody] CreateLobbyRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            service.CreateLobby(
                new CreateLobbyRequest
                    {   AuthorId = "admin",
                        LobbyName = Guid.NewGuid().ToString(),
                        LobbyPassword = "dota2" 
                    }
            );

            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error creating lobby: {ex.Message}");
            return TypedResults.StatusCode(500);
        }
    }

    private static Results<Ok, StatusCodeHttpResult> InvitePlayersToLobby(
        Services.Dota2Service service,
        [FromBody] InviteRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            service.InvitePlayersToLobby(request.PlayersIds);

            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error inviting players to lobby: {ex.Message}");
            return TypedResults.StatusCode(500);
        }
    }
}
