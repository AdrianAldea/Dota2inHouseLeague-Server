using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Mvc;

namespace IHLDota2Bot.Endpoints.GameManager;

/// <summary>
/// Admin endpoints. Require authentication via <c>X-Api-Key</c> header (see
/// <c>MapGameManagerEndpoints</c> and <c>AdminApiKeyAuthenticationHandler</c>).
/// </summary>
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost(
            "/admin/ban/{id}",
            (
                IDota2Service svc,
                ILogger<Dota2Service> log,
                [FromRoute] ulong id,
                [FromQuery] string? reason,
                CancellationToken ct
            ) =>
                EndpointHelpers.TryOkAsync(
                    log,
                    "ban player",
                    () => svc.BanPlayerAsync(id, reason, ct)
                )
        );

        group.MapPost(
            "/admin/unban/{id}",
            (
                IDota2Service svc,
                ILogger<Dota2Service> log,
                [FromRoute] ulong id,
                CancellationToken ct
            ) => EndpointHelpers.TryOkAsync(log, "unban player", () => svc.UnbanPlayerAsync(id, ct))
        );

        group.MapPost(
            "/admin/kick/{id}",
            (IDota2Service svc, ILogger<Dota2Service> log, [FromRoute] ulong id) =>
                EndpointHelpers.TryOkAsync(log, "kick player", () => svc.KickPlayerAsync(id))
        );

        group.MapPost(
            "/admin/force_start",
            (IDota2Service svc, ILogger<Dota2Service> log) =>
                EndpointHelpers.TryOkAsync(log, "force start lobby", svc.ForceStartLobbyAsync)
        );

        group.MapPost(
            "/admin/reset_queue",
            (IDota2Service svc, ILogger<Dota2Service> log) =>
                EndpointHelpers.TryOkAsync(log, "reset queue", svc.ResetQueueAsync)
        );

        group.MapPost(
            "/admin/add_account",
            (IDota2Service svc, ILogger<Dota2Service> log, [FromBody] AddSteamAccountRequest req) =>
                EndpointHelpers.TryOkAsync(
                    log,
                    "add Steam account",
                    () => svc.AddSteamAccountAsync(req.Username, req.Password)
                )
        );

        group.MapGet(
            "/admin/pool_status",
            (IDota2Service svc, ILogger<Dota2Service> log) =>
                EndpointHelpers.TryOk(log, "get pool status", svc.GetPoolStatus)
        );

        group.MapGet(
            "/admin/sessions",
            (IDota2Service svc, ILogger<Dota2Service> log) =>
                EndpointHelpers.TryOk(log, "get active sessions", svc.GetActiveSessions)
        );

        // Manual overrides — useful if a match was reported through some other
        // channel and the bot didn't auto-close the loop.
        group.MapPost(
            "/admin/game/started/{lobbyId}",
            async (IDota2Service svc, ILogger<Dota2Service> log, [FromRoute] string lobbyId) =>
                await EndpointHelpers.TryOkAsync(
                    log,
                    "mark game started",
                    async () =>
                    {
                        await svc.NotifyGameStartedAsync(lobbyId);
                        return new { lobbyId, status = "started" };
                    }
                )
        );

        group.MapPost(
            "/admin/game/finished/{lobbyId}",
            async (IDota2Service svc, ILogger<Dota2Service> log, [FromRoute] string lobbyId) =>
                await EndpointHelpers.TryOkAsync(
                    log,
                    "mark game finished",
                    async () =>
                    {
                        await svc.NotifyGameFinishedAsync(lobbyId);
                        return new { lobbyId, status = "finished" };
                    }
                )
        );

        // Destroy — force the hosted game to end: sends the Dota GC a
        // PracticeLobbyLeave on the lobby's bot, then releases the account and
        // reopens the queue. Unlike /game/finished, this also tears the lobby
        // down on Steam's side.
        group.MapPost(
            "/admin/game/destroy/{lobbyId}",
            async (IDota2Service svc, ILogger<Dota2Service> log, [FromRoute] string lobbyId) =>
                await EndpointHelpers.TryOkAsync(
                    log,
                    "destroy lobby",
                    async () =>
                    {
                        await svc.DestroyLobbyAsync(lobbyId);
                        return new { lobbyId, status = "destroyed" };
                    }
                )
        );

        return group;
    }
}
