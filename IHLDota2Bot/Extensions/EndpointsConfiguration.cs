using IHLDota2Bot.Endpoints.Auth;
using IHLDota2Bot.Endpoints.GameManager;
using IHLDota2Bot.Endpoints.StartUp;
using IHLDota2Bot.Extensions.RateLimiting;

namespace IHLDota2Bot.Extensions;

public static class EndpointsConfiguration
{
    public static WebApplication ConfigureEndpoints(this WebApplication app)
    {
        // /server/start boots the Steam bot pool — an operational control, so it
        // sits behind the same X-Api-Key gate as the admin routes.
        app.MapGroup("/api/server").RequireAuthorization().MapStartUpEndpoints();
        app.MapGroup("/api/gamemanager").MapGameManagerEndpoints();
        // The auth group is the unauthenticated, internet-facing surface (Steam
        // login + registration), so it carries the player rate-limit policy.
        app.MapGroup("/api/auth")
            .RequireRateLimiting(PlayerRateLimiting.PolicyName)
            .MapAuthEndpoints();

        return app;
    }
}
