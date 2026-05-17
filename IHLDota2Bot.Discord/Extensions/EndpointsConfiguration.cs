using IHLDota2Bot.Discord.Discord;

namespace IHLDota2Bot.Discord.Extensions;

public static class EndpointsConfiguration
{
    public static WebApplication ConfigureEndpoints(this WebApplication app)
    {
        app.MapGroup("/api/server").MapDiscordStartUpEndpoints();
        
        return app;
    }

}