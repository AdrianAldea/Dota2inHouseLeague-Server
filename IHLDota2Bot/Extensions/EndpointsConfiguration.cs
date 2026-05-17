﻿using IHLDota2Bot.Endpoints.GameManager;
using IHLDota2Bot.Endpoints.StartUp;
using IHLDota2Bot.Endpoints.Discord;

namespace Dota2Bot.Extensions;

public static class EndpointsConfiguration
{
    public static WebApplication ConfigureEndpoints(this WebApplication app)
    {
        app.MapGroup("/api/server").MapStartUpEndpoints();
        //app.MapGroup("/api/discord").MapDiscordStartUpEndpoints();
        app.MapGroup("/api/gamemanager").MapGameManagerEndpoints();
        
        return app;
    }

}