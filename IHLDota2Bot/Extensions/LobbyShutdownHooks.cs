using IHLDota2Bot.Services;

namespace IHLDota2Bot.Extensions;

public static class LobbyShutdownHooks
{
    /// <summary>
    /// Crash / hard-shutdown safety net: <see cref="IHostedService.StopAsync"/>
    /// only runs on a graceful shutdown, so a SIGTERM on the host or an unhandled
    /// exception would otherwise leave the Dota GC holding a ghost lobby. Hook
    /// <c>ProcessExit</c> and <c>UnhandledException</c> to send PracticeLobbyLeave
    /// for every active bot.
    /// </summary>
    public static WebApplication UseLobbyShutdownHooks(this WebApplication app)
    {
        var steamPool = app.Services.GetRequiredService<SteamGcClientPool>();
        var logger = app
            .Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("LobbyShutdownHook");

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            logger.LogInformation("ProcessExit fired; destroying active lobbies");
            steamPool.DestroyAllActiveLobbies();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            logger.LogError(
                e.ExceptionObject as Exception,
                "UnhandledException; destroying active lobbies"
            );
            steamPool.DestroyAllActiveLobbies();
        };

        return app;
    }
}
