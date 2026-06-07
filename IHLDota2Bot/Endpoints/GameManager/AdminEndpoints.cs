using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Mvc;

namespace IHLDota2Bot.Endpoints.GameManager;

/// <summary>
/// Admin endpoints (server-side gate not yet enforced — Discord layer
/// gates by user id / role for now).
/// </summary>
public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/admin/ban/{id}",
            (Dota2Service svc, ILogger<Dota2Service> log, [FromRoute] ulong id, [FromQuery] string? reason, CancellationToken ct) =>
                EndpointHelpers.TryOkAsync(log, "ban player", () => svc.BanPlayerAsync(id, reason, ct)));

        group.MapPost("/admin/unban/{id}",
            (Dota2Service svc, ILogger<Dota2Service> log, [FromRoute] ulong id, CancellationToken ct) =>
                EndpointHelpers.TryOkAsync(log, "unban player", () => svc.UnbanPlayerAsync(id, ct)));

        group.MapPost("/admin/kick/{id}",
            (Dota2Service svc, ILogger<Dota2Service> log, [FromRoute] ulong id, CancellationToken ct) =>
                EndpointHelpers.TryOkAsync(log, "kick player", () => svc.KickPlayerAsync(id)));

        group.MapPost("/admin/force_start",
            (Dota2Service svc, ILogger<Dota2Service> log, CancellationToken ct) =>
                EndpointHelpers.TryOkAsync(log, "force start lobby", svc.ForceStartLobbyAsync));

        group.MapPost("/admin/reset_queue",
            (Dota2Service svc, ILogger<Dota2Service> log, CancellationToken ct) =>
                EndpointHelpers.TryOkAsync(log, "reset queue", svc.ResetQueueAsync));

        group.MapPost("/admin/add_account",
            (Dota2Service svc, ILogger<Dota2Service> log, [FromBody] AddSteamAccountRequest req, CancellationToken ct) =>
                EndpointHelpers.TryOkAsync(log, "add Steam account", () => svc.AddSteamAccountAsync(req.Username, req.Password)));

        group.MapGet("/admin/pool_status",
            (Dota2Service svc, ILogger<Dota2Service> log) =>
                EndpointHelpers.TryOk(log, "get pool status", svc.GetPoolStatus));

        group.MapGet("/admin/sessions",
            (Dota2Service svc, ILogger<Dota2Service> log) =>
                EndpointHelpers.TryOk(log, "get active sessions", svc.GetActiveSessions));

        // Manual overrides — useful if a match was reported through some other
        // channel and the bot didn't auto-close the loop.
        group.MapPost("/admin/game/started/{lobbyId}",
            async (Dota2Service svc, ILogger<Dota2Service> log, [FromRoute] string lobbyId) =>
                await EndpointHelpers.TryOkAsync(log, "mark game started", async () =>
                {
                    await svc.NotifyGameStartedAsync(lobbyId);
                    return new { lobbyId, status = "started" };
                }));

        group.MapPost("/admin/game/finished/{lobbyId}",
            async (Dota2Service svc, ILogger<Dota2Service> log, [FromRoute] string lobbyId) =>
                await EndpointHelpers.TryOkAsync(log, "mark game finished", async () =>
                {
                    await svc.NotifyGameFinishedAsync(lobbyId);
                    return new { lobbyId, status = "finished" };
                }));

        return group;
    }
}
