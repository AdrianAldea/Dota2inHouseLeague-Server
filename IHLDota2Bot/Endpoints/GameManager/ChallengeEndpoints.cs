using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Mvc;

namespace IHLDota2Bot.Endpoints.GameManager;

/// <summary>
/// 1v1 challenge invite endpoints.
/// </summary>
public static class ChallengeEndpoints
{
    public static RouteGroupBuilder MapChallengeEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/challenge/{challengerId}/{challengedId}",
            (IDota2Service svc, ILogger<Dota2Service> log, [FromRoute] ulong challengerId, [FromRoute] ulong challengedId, CancellationToken ct) =>
                EndpointHelpers.TryOkAsync(log, "create challenge", () => svc.ChallengePlayerAsync(challengerId, challengedId, ct)));

        group.MapPost("/challenge/accept/{userId}",
            (IDota2Service svc, ILogger<Dota2Service> log, [FromRoute] ulong userId, CancellationToken ct) =>
                EndpointHelpers.TryOkAsync(log, "accept challenge", () => svc.AcceptChallengeAsync(userId, ct)));

        group.MapPost("/challenge/decline/{userId}",
            (IDota2Service svc, ILogger<Dota2Service> log, [FromRoute] ulong userId, CancellationToken ct) =>
                EndpointHelpers.TryOkAsync(log, "decline challenge", () => svc.DeclineChallengeAsync(userId, ct)));

        return group;
    }
}
