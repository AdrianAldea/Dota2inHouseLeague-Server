using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Mvc;

namespace IHLDota2Bot.Endpoints.GameManager;

/// <summary>
/// Player lifecycle + read endpoints: register / profile / unregister / link / stats / rank.
/// </summary>
public static class PlayerEndpoints
{
    public static RouteGroupBuilder MapPlayerEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost(
            "/register",
            (
                IDota2Service svc,
                ILogger<Dota2Service> log,
                [FromBody] RegisterPlayerRequest req,
                CancellationToken ct
            ) =>
                EndpointHelpers.TryOkAsync(
                    log,
                    "register player",
                    () => svc.RegisterPlayerAsync(req, ct)
                )
        );

        group.MapGet(
            "/player/{id}",
            (
                IDota2Service svc,
                ILogger<Dota2Service> log,
                [FromRoute] ulong id,
                CancellationToken ct
            ) =>
                EndpointHelpers.TryFindAsync(
                    log,
                    "get player profile",
                    () => svc.GetPlayerProfileAsync(id, ct)
                )
        );

        group.MapPost(
            "/unregister/{id}",
            (
                IDota2Service svc,
                ILogger<Dota2Service> log,
                [FromRoute] ulong id,
                CancellationToken ct
            ) =>
                EndpointHelpers.TryOkAsync(
                    log,
                    "unregister player",
                    () => svc.UnregisterPlayerAsync(id, ct)
                )
        );

        group.MapPost(
            "/link/{id}/{steamId}",
            (
                IDota2Service svc,
                ILogger<Dota2Service> log,
                [FromRoute] ulong id,
                [FromRoute] ulong steamId,
                CancellationToken ct
            ) =>
                EndpointHelpers.TryOkAsync(
                    log,
                    "link Steam ID",
                    () => svc.LinkSteamIdAsync(id, steamId, ct)
                )
        );

        group.MapGet(
            "/stats/{id}",
            (
                IDota2Service svc,
                ILogger<Dota2Service> log,
                [FromRoute] ulong id,
                CancellationToken ct
            ) =>
                EndpointHelpers.TryFindAsync(
                    log,
                    "get player stats",
                    () => svc.GetPlayerStatsAsync(id, ct)
                )
        );

        group.MapGet(
            "/leaderboard",
            (
                IDota2Service svc,
                ILogger<Dota2Service> log,
                [FromQuery] int? top,
                CancellationToken ct
            ) =>
                EndpointHelpers.TryOkAsync(
                    log,
                    "get leaderboard",
                    () => svc.GetLeaderboardAsync(top ?? 10, ct)
                )
        );

        group.MapGet(
            "/rank/{id}",
            (
                IDota2Service svc,
                ILogger<Dota2Service> log,
                [FromRoute] ulong id,
                CancellationToken ct
            ) =>
                EndpointHelpers.TryFindAsync(
                    log,
                    "get player rank",
                    () => svc.GetPlayerRankAsync(id, ct)
                )
        );

        return group;
    }
}
