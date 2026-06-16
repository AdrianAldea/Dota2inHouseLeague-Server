using IHLDota2Bot.Auth;
using IHLDota2Bot.Clients;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Data;
using IHLDota2Bot.Data.Repositories;
using IHLDota2Bot.Services;
using IHLDota2Bot.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;

namespace IHLDota2Bot.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything the Game Manager needs: configuration options, the
    /// Discord HTTP client (resolved via Aspire service discovery), the EF Core
    /// DbContext factory, repositories, the lobby/pool system, and the hosted
    /// services that drive them. Keeps Program.cs to a readable startup outline.
    /// </summary>
    public static IServiceCollection AddGameManager(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SteamOptions>(configuration.GetSection(SteamOptions.SectionName));
        services.Configure<DiscordClientOptions>(configuration.GetSection(DiscordClientOptions.SectionName));
        services.Configure<AdminApiKeyOptions>(configuration.GetSection(AdminApiKeyOptions.SectionName));

        services
            .AddAuthentication(AdminApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<AdminApiKeyAuthenticationHandler.SchemeOptions, AdminApiKeyAuthenticationHandler>(
                AdminApiKeyAuthenticationHandler.SchemeName, _ => { });
        services.AddAuthorization();

        services.AddHttpClient(DiscordClient.HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<DiscordClientOptions>>().Value;
            client.BaseAddress = new Uri($"https+http://{options.ServiceName}/");
        });

        services.AddDbContextFactory<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("Dota2inHouseLeagueDB")
                ?? throw new InvalidOperationException("Connection string 'Dota2inHouseLeagueDB' is not configured.");
            // A freshly-provisioned SQL Server container accepts TCP connections before the
            // engine is ready for the login handshake, so the first connections can fail
            // transiently ("connection forcibly closed during pre-login handshake"). Retry
            // on transient failures so startup rides out the container warming up.
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null));
        });

        services.AddSingleton<IPlayerRepository, PlayerRepository>();
        services.AddSingleton<IMatchRepository, MatchRepository>();
        services.AddSingleton<IChallengeRepository, ChallengeRepository>();
        services.AddHostedService<DatabaseInitializer>();

        // Domain telemetry: register the meter singleton and subscribe the OTel
        // metrics pipeline to it.
        services.AddSingleton<GameMetrics>();
        services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(GameMetrics.MeterName));

        services.AddSingleton<DiscordClient>();
        services.AddSingleton<SteamAccountPool>();
        services.AddSingleton<LobbyQueue>();
        services.AddSingleton<SteamGcClientPool>();
        services.AddHostedService(sp => sp.GetRequiredService<SteamGcClientPool>());
        services.AddSingleton<LobbyPoolManager>();
        services.AddSingleton<IDota2Service, Dota2Service>();
        services.AddHostedService(sp => sp.GetRequiredService<LobbyPoolManager>());

        return services;
    }
}
