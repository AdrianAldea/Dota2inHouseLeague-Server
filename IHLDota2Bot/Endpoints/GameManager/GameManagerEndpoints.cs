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
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await service.MarkPlayerAsReadyAsync(id);
            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error marking player as ready");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<PlayerProfile>, NotFound, StatusCodeHttpResult>> GetPlayerProfile(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.GetPlayerProfileAsync(id);
            return result == null ? TypedResults.NotFound() : TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving player profile");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<RegisteredPlayer>, StatusCodeHttpResult>> RegisterPlayer(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromBody] RegisterPlayerRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.RegisterPlayerAsync(request);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering player");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok, StatusCodeHttpResult>> CreateLobby(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromBody] CreateLobbyRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await service.CreateLobbyAsync(request);
            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating lobby");
            return TypedResults.StatusCode(500);
        }
    }

    private static Results<Ok, StatusCodeHttpResult> InvitePlayersToLobby(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
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
            logger.LogError(ex, "Error inviting players to lobby");
            return TypedResults.StatusCode(500);
        }
    }
}
