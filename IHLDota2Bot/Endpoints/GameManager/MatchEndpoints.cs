using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace IHLDota2Bot.Endpoints.GameManager;

/// <summary>
/// Match endpoints: current / by-id / recent / report.
/// </summary>
public static class MatchEndpoints
{
    public static RouteGroupBuilder MapMatchEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/match/current/{id}",
            (IDota2Service svc, ILogger<Dota2Service> log, [FromRoute] ulong id, CancellationToken ct) =>
                EndpointHelpers.TryFindAsync(log, "get current match", () => svc.GetCurrentMatchAsync(id, ct)));

        group.MapGet("/match/{matchId:guid}",
            (IDota2Service svc, ILogger<Dota2Service> log, [FromRoute] Guid matchId, CancellationToken ct) =>
                EndpointHelpers.TryFindAsync(log, "get match by id", () => svc.GetMatchByIdAsync(matchId, ct)));

        group.MapGet("/match/by-lobby/{lobbyId}",
            (IDota2Service svc, ILogger<Dota2Service> log, [FromRoute] string lobbyId, CancellationToken ct) =>
                EndpointHelpers.TryFindAsync(log, "get match by lobby", () => svc.GetMatchByLobbyAsync(lobbyId, ct)));

        group.MapGet("/match/recent/{id}",
            (IDota2Service svc, ILogger<Dota2Service> log, [FromRoute] ulong id, [FromQuery] int? top, CancellationToken ct) =>
                EndpointHelpers.TryOkAsync(log, "get recent matches", () => svc.GetRecentMatchesAsync(id, top ?? 5, ct)));

        group.MapPost("/match/report/{id}/{winner}", ReportMatch);

        return group;
    }

    private static async Task<Results<Ok<QueueOperationResponse>, BadRequest<string>, StatusCodeHttpResult>> ReportMatch(
        IDota2Service svc,
        ILogger<Dota2Service> log,
        [FromRoute] ulong id,
        [FromRoute] string winner,
        CancellationToken ct)
    {
        if (!Enum.TryParse<MatchTeam>(winner, ignoreCase: true, out var team))
            return TypedResults.BadRequest("Winner must be 'radiant' or 'dire'.");

        try
        {
            var result = await svc.ReportMatchAsync(id, team, ct);
            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error reporting match");
            return TypedResults.StatusCode(500);
        }
    }
}
