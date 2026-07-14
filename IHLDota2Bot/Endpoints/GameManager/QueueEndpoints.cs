using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Mvc;

namespace IHLDota2Bot.Endpoints.GameManager;

/// <summary>
/// Queue / ready-state endpoints. The id-based mutations act on arbitrary players,
/// so they map onto the API-key-gated <paramref name="service"/> group (Discord bot /
/// admin panel); players use the JWT me-endpoints. The id-free status read stays public.
/// </summary>
public static class QueueEndpoints
{
    public static RouteGroupBuilder MapQueueEndpoints(
        this RouteGroupBuilder group,
        RouteGroupBuilder service
    )
    {
        service.MapPost(
            "/queue/{id}",
            (
                IDota2Service svc,
                ILogger<Dota2Service> log,
                [FromRoute] ulong id,
                CancellationToken ct
            ) => EndpointHelpers.TryOkAsync(log, "join queue", () => svc.JoinQueueAsync(id, ct))
        );

        service.MapPost(
            "/leave/{id}",
            (IDota2Service svc, ILogger<Dota2Service> log, [FromRoute] ulong id) =>
                EndpointHelpers.TryOkAsync(log, "leave queue", () => svc.LeaveQueueAsync(id))
        );

        service.MapPost(
            "/ready/{id}",
            (IDota2Service svc, ILogger<Dota2Service> log, [FromRoute] ulong id) =>
                EndpointHelpers.TryOkAsync(
                    log,
                    "mark player ready",
                    () => svc.MarkPlayerAsReadyAsync(id)
                )
        );

        service.MapPost(
            "/unready/{id}",
            (IDota2Service svc, ILogger<Dota2Service> log, [FromRoute] ulong id) =>
                EndpointHelpers.TryOkAsync(
                    log,
                    "mark player unready",
                    () => svc.MarkPlayerAsUnreadyAsync(id)
                )
        );

        group.MapGet(
            "/queue/status",
            (IDota2Service svc, ILogger<Dota2Service> log) =>
                EndpointHelpers.TryOk(log, "get queue status", svc.GetQueueStatus)
        );

        return group;
    }
}
