using System.Linq;
using System.Security.Claims;
using IHLDota2Bot.Auth;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Data.Repositories;
using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Mvc;

namespace IHLDota2Bot.Endpoints.Auth;

/// <summary>
/// Steam login + player identity endpoints, mapped at <c>/api/auth</c>:
/// <c>/steam/login</c> → redirect to Steam; <c>/steam/callback</c> → verify + issue JWT
/// and hand the token back to the desktop client; <c>/register</c> → create a league
/// account for a new Steam user; <c>/me</c> → who am I. No Discord involved.
/// </summary>
public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        // 1) Kick off Steam auth: redirect the browser to Steam, asking it to return to
        //    our callback (which carries the desktop loopback returnUrl as state).
        group.MapGet(
            "/steam/login",
            (HttpContext ctx, SteamOpenIdService steam, [FromQuery] string returnUrl) =>
            {
                var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
                var returnTo =
                    $"{baseUrl}/api/auth/steam/callback?returnUrl={Uri.EscapeDataString(returnUrl)}";
                return Results.Redirect(steam.BuildRedirectUrl(baseUrl, returnTo));
            }
        );

        // 2) Steam redirects here. Verify, resolve/identify the player, issue a JWT, and
        //    bounce back to the desktop loopback with ?token=...&registered=...
        group.MapGet(
            "/steam/callback",
            async (
                HttpContext ctx,
                SteamOpenIdService steam,
                PlayerTokenService tokens,
                IPlayerRepository players,
                ILogger<SteamOpenIdService> log,
                [FromQuery] string returnUrl,
                CancellationToken ct
            ) =>
            {
                var query = ctx.Request.Query.Select(kv => new KeyValuePair<string, string>(
                    kv.Key,
                    kv.Value.ToString()
                ));
                var steamId = await steam.VerifyAsync(query, ct);

                if (steamId is null)
                {
                    log.LogWarning("Steam OpenID verification failed.");
                    return Results.Redirect(
                        AppendQuery(returnUrl, "error", "steam_verification_failed")
                    );
                }

                var player = await players.GetBySteamIdAsync(steamId, ct);
                string token;
                bool registered;
                if (player is not null)
                {
                    token = tokens.CreateToken(
                        player.UserId,
                        steamId,
                        player.Username,
                        registered: true
                    );
                    registered = true;
                }
                else
                {
                    // Unregistered: identity = SteamID64; client will prompt for a display name.
                    token = tokens.CreateToken(
                        ulong.Parse(steamId),
                        steamId,
                        username: null,
                        registered: false
                    );
                    registered = false;
                }

                var url = AppendQuery(
                    AppendQuery(returnUrl, "token", token),
                    "registered",
                    registered ? "true" : "false"
                );
                return Results.Redirect(url);
            }
        );

        // 3) Complete registration for a new Steam user (token present, registered=false).
        group
            .MapPost(
                "/register",
                async (
                    ClaimsPrincipal user,
                    IDota2Service svc,
                    PlayerTokenService tokens,
                    [FromBody] RegisterAccountRequest req,
                    CancellationToken ct
                ) =>
                {
                    var steamId = user.FindFirstValue(PlayerClaims.SteamId);
                    if (string.IsNullOrEmpty(steamId) || !ulong.TryParse(steamId, out var id))
                        return Results.Unauthorized();

                    var username = string.IsNullOrWhiteSpace(req.Username)
                        ? $"Player{steamId[^Math.Min(4, steamId.Length)..]}"
                        : req.Username.Trim();

                    await svc.RegisterPlayerAsync(
                        new RegisterPlayerRequest
                        {
                            SteamId = id,
                            UserId = id,
                            Username = username,
                        },
                        ct
                    );

                    var profile = await svc.GetPlayerStatsAsync(id, ct);
                    var token = tokens.CreateToken(id, steamId, username, registered: true);
                    return Results.Ok(
                        new AuthResultResponse
                        {
                            Token = token,
                            Registered = true,
                            Profile = profile,
                        }
                    );
                }
            )
            .RequireAuthorization("Player");

        // 4) Identity behind the current token.
        group
            .MapGet(
                "/me",
                async (ClaimsPrincipal user, IDota2Service svc, CancellationToken ct) =>
                {
                    var sub = user.FindFirstValue(PlayerClaims.Subject);
                    if (sub is null || !ulong.TryParse(sub, out var userId))
                        return Results.Unauthorized();

                    var steamId = user.FindFirstValue(PlayerClaims.SteamId) ?? sub;
                    var profile = await svc.GetPlayerStatsAsync(userId, ct);
                    return Results.Ok(
                        new MeResponse
                        {
                            UserId = userId,
                            SteamId = steamId,
                            Registered = profile is not null,
                            Profile = profile,
                        }
                    );
                }
            )
            .RequireAuthorization("Player");

        return group;
    }

    /// <summary>Appends a query param, choosing <c>?</c> or <c>&amp;</c> as appropriate.</summary>
    private static string AppendQuery(string url, string key, string value)
    {
        var separator = url.Contains('?') ? '&' : '?';
        return $"{url}{separator}{key}={Uri.EscapeDataString(value)}";
    }
}
