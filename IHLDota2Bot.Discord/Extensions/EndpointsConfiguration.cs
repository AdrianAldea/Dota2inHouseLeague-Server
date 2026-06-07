using IHLDota2Bot.Discord.Endpoints;

namespace IHLDota2Bot.Discord.Extensions;

public static class EndpointsConfiguration
{
    public static WebApplication ConfigureEndpoints(this WebApplication app)
    {
        app.MapGroup("/api").MapStartUpEndpoints();
        return app;
    }
}
