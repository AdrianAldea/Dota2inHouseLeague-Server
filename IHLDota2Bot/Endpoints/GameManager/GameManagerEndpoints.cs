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
        group.MapPost("/unready/{id}", MarkPlayerAsUnready);
        group.MapPost("/queue/{id}", JoinQueue);
        group.MapPost("/leave/{id}", LeaveQueue);
        group.MapGet("/queue/status", GetQueueStatus);
        group.MapPost("/unregister/{id}", UnregisterPlayer);
        group.MapPost("/link/{id}/{steamId}", LinkSteamId);
        group.MapGet("/stats/{id}", GetPlayerStats);
        group.MapGet("/leaderboard", GetLeaderboard);
        group.MapGet("/match/current/{id}", GetCurrentMatch);
        group.MapGet("/match/recent/{id}", GetRecentMatches);
        group.MapPost("/match/report/{id}/{winner}", ReportMatch);
        group.MapPost("/challenge/{challengerId}/{challengedId}", ChallengePlayer);
        group.MapPost("/challenge/accept/{userId}", AcceptChallenge);
        group.MapPost("/challenge/decline/{userId}", DeclineChallenge);
        group.MapPost("/invite", InvitePlayersToLobby);
    }

    private static async Task<Results<Ok<QueueOperationResponse>, StatusCodeHttpResult>> MarkPlayerAsReady(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.MarkPlayerAsReadyAsync(id);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error marking player as ready");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<QueueOperationResponse>, StatusCodeHttpResult>> MarkPlayerAsUnready(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.MarkPlayerAsUnreadyAsync(id);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error marking player as unready");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<QueueOperationResponse>, StatusCodeHttpResult>> JoinQueue(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.JoinQueueAsync(id, cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error joining queue");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<QueueOperationResponse>, StatusCodeHttpResult>> LeaveQueue(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.LeaveQueueAsync(id);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error leaving queue");
            return TypedResults.StatusCode(500);
        }
    }

    private static Results<Ok<QueueStatusResponse>, StatusCodeHttpResult> GetQueueStatus(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger
    )
    {
        try
        {
            return TypedResults.Ok(service.GetQueueStatus());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting queue status");
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
            var result = await service.GetPlayerProfileAsync(id, cancellationToken);
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
            var result = await service.RegisterPlayerAsync(request, cancellationToken);
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

    private static async Task<Results<Ok<QueueOperationResponse>, StatusCodeHttpResult>> UnregisterPlayer(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.UnregisterPlayerAsync(id, cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unregistering player");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<QueueOperationResponse>, StatusCodeHttpResult>> LinkSteamId(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong id,
        [FromRoute] ulong steamId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.LinkSteamIdAsync(id, steamId, cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error linking Steam ID");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<PlayerStatsResponse>, NotFound, StatusCodeHttpResult>> GetPlayerStats(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.GetPlayerStatsAsync(id, cancellationToken);
            return result == null ? TypedResults.NotFound() : TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving player stats");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<LeaderboardResponse>, StatusCodeHttpResult>> GetLeaderboard(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromQuery] int? top,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.GetLeaderboardAsync(top ?? 10, cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving leaderboard");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<MatchSummary>, NotFound, StatusCodeHttpResult>> GetCurrentMatch(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var match = await service.GetCurrentMatchAsync(id, cancellationToken);
            return match == null ? TypedResults.NotFound() : TypedResults.Ok(match);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting current match");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<RecentMatchesResponse>, StatusCodeHttpResult>> GetRecentMatches(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong id,
        [FromQuery] int? top,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.GetRecentMatchesAsync(id, top ?? 5, cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting recent matches");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<QueueOperationResponse>, BadRequest<string>, StatusCodeHttpResult>> ReportMatch(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong id,
        [FromRoute] string winner,
        CancellationToken cancellationToken
    )
    {
        if (!Enum.TryParse<MatchTeam>(winner, ignoreCase: true, out var team))
        {
            return TypedResults.BadRequest("Winner must be 'radiant' or 'dire'.");
        }

        try
        {
            var result = await service.ReportMatchAsync(id, team, cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reporting match");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<ChallengeOperationResponse>, StatusCodeHttpResult>> ChallengePlayer(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong challengerId,
        [FromRoute] ulong challengedId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.ChallengePlayerAsync(challengerId, challengedId, cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating challenge");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<ChallengeOperationResponse>, StatusCodeHttpResult>> AcceptChallenge(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong userId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.AcceptChallengeAsync(userId, cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error accepting challenge");
            return TypedResults.StatusCode(500);
        }
    }

    private static async Task<Results<Ok<ChallengeOperationResponse>, StatusCodeHttpResult>> DeclineChallenge(
        Services.Dota2Service service,
        ILogger<Services.Dota2Service> logger,
        [FromRoute] ulong userId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await service.DeclineChallengeAsync(userId, cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error declining challenge");
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
