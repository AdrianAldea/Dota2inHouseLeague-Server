using System.Security.Claims;
using IHLDota2Bot.Auth;
using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Mvc;

namespace IHLDota2Bot.Endpoints.GameManager;

/// <summary>
/// JWT-protected "act as the signed-in player" endpoints. Identity comes from the token
/// (<c>sub</c>), never a route id, so a player can only queue/ready/etc. as themselves.
/// These sit alongside the legacy <c>/{id}</c> routes used by admin/back-compat callers.
/// </summary>
public static class MeEndpoints
{
    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder group)
    {
        var me = group.MapGroup(string.Empty).RequireAuthorization("Player");

        // A token whose `sub` doesn't parse as a user id carries no usable identity —
        // reject it outright rather than letting UserId() fall back to 0 and act as a
        // phantom "user 0". PlayerTokenService always writes a numeric sub, so this
        // only fires for malformed/foreign tokens; it's defence in depth.
        me.AddEndpointFilter(
            async (ctx, next) =>
                ulong.TryParse(ctx.HttpContext.User.FindFirstValue(PlayerClaims.Subject), out _)
                    ? await next(ctx)
                    : TypedResults.Unauthorized()
        );

        me.MapPost(
            "/queue/join",
            (
                ClaimsPrincipal user,
                IDota2Service svc,
                ILogger<Dota2Service> log,
                CancellationToken ct
            ) =>
                EndpointHelpers.TryOkAsync(
                    log,
                    "join queue (me)",
                    () => svc.JoinQueueAsync(UserId(user), ct)
                )
        );

        me.MapPost(
            "/queue/leave",
            (ClaimsPrincipal user, IDota2Service svc, ILogger<Dota2Service> log) =>
                EndpointHelpers.TryOkAsync(
                    log,
                    "leave queue (me)",
                    () => svc.LeaveQueueAsync(UserId(user))
                )
        );

        me.MapPost(
            "/ready",
            (ClaimsPrincipal user, IDota2Service svc, ILogger<Dota2Service> log) =>
                EndpointHelpers.TryOkAsync(
                    log,
                    "ready (me)",
                    () => svc.MarkPlayerAsReadyAsync(UserId(user))
                )
        );

        me.MapPost(
            "/unready",
            (ClaimsPrincipal user, IDota2Service svc, ILogger<Dota2Service> log) =>
                EndpointHelpers.TryOkAsync(
                    log,
                    "unready (me)",
                    () => svc.MarkPlayerAsUnreadyAsync(UserId(user))
                )
        );

        me.MapGet(
            "/me/stats",
            (
                ClaimsPrincipal user,
                IDota2Service svc,
                ILogger<Dota2Service> log,
                CancellationToken ct
            ) =>
                EndpointHelpers.TryFindAsync(
                    log,
                    "me stats",
                    () => svc.GetPlayerStatsAsync(UserId(user), ct)
                )
        );

        me.MapGet(
            "/me/rank",
            (
                ClaimsPrincipal user,
                IDota2Service svc,
                ILogger<Dota2Service> log,
                CancellationToken ct
            ) =>
                EndpointHelpers.TryFindAsync(
                    log,
                    "me rank",
                    () => svc.GetPlayerRankAsync(UserId(user), ct)
                )
        );

        me.MapGet(
            "/me/matches",
            (
                ClaimsPrincipal user,
                IDota2Service svc,
                ILogger<Dota2Service> log,
                [FromQuery] int? top,
                CancellationToken ct
            ) =>
                EndpointHelpers.TryOkAsync(
                    log,
                    "me matches",
                    () => svc.GetRecentMatchesAsync(UserId(user), top ?? 5, ct)
                )
        );

        return group;
    }

    /// <summary>Parses the token's subject; the group's endpoint filter guarantees it parses.</summary>
    private static ulong UserId(ClaimsPrincipal user) =>
        ulong.Parse(user.FindFirstValue(PlayerClaims.Subject)!);
}
