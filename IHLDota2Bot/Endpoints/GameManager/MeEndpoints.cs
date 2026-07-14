using System.Security.Claims;
using IHLDota2Bot.Auth;
using IHLDota2Bot.Common.DTOs;
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
        var me = group
            .MapGroup(string.Empty)
            .RequireAuthorization("Player")
            .RequireRateLimiting(Extensions.RateLimiting.PlayerRateLimiting.PolicyName);

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

        // The player's own queue membership — lets the client reconcile its
        // optimistic in-queue state with server truth (e.g. after a restart)
        // without the public queue status exposing user ids.
        me.MapGet(
            "/me/queue",
            (ClaimsPrincipal user, IDota2Service svc, ILogger<Dota2Service> log) =>
                EndpointHelpers.TryOk(
                    log,
                    "me queue state",
                    () => svc.GetPlayerQueueState(UserId(user))
                )
        );

        // The player's current in-progress match, including the Dota lobby
        // credentials — the client-side fallback for a missed Steam invite.
        // Credentials derive from the lobby id (see LobbyNaming), so nothing
        // extra is persisted, and only the match's own participants can get
        // them (the lookup is keyed on the token's user id).
        me.MapGet(
            "/me/match",
            (
                ClaimsPrincipal user,
                IDota2Service svc,
                ILogger<Dota2Service> log,
                CancellationToken ct
            ) =>
                EndpointHelpers.TryFindAsync<CurrentMatchResponse>(
                    log,
                    "me current match",
                    async () =>
                    {
                        var match = await svc.GetCurrentMatchAsync(UserId(user), ct);
                        if (match is null)
                            return null;

                        var credentials = match.LobbyId is { Length: > 0 } lobbyId
                            ? LobbyNaming.For(lobbyId)
                            : null;
                        return new CurrentMatchResponse
                        {
                            Match = match,
                            LobbyName = credentials,
                            LobbyPassword = credentials,
                        };
                    }
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
