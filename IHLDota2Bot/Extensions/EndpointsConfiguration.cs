using IHLDota2Bot.Endpoints.Auth;
using IHLDota2Bot.Endpoints.GameManager;
using IHLDota2Bot.Endpoints.StartUp;

namespace IHLDota2Bot.Extensions;

public static class EndpointsConfiguration
{
    public static WebApplication ConfigureEndpoints(this WebApplication app)
    {
        app.MapGroup("/api/server").MapStartUpEndpoints();
        app.MapGroup("/api/gamemanager").MapGameManagerEndpoints();
        app.MapGroup("/api/auth").MapAuthEndpoints();

        return app;
    }

}