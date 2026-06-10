using IHLDota2Bot;
using IHLDota2Bot.Clients;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Data;
using IHLDota2Bot.Data.Repositories;
using IHLDota2Bot.Extensions;
using IHLDota2Bot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<SteamOptions>(builder.Configuration.GetSection(SteamOptions.SectionName));
builder.Services.Configure<DiscordClientOptions>(builder.Configuration.GetSection(DiscordClientOptions.SectionName));

builder.Services.AddHttpClient(DiscordClient.HttpClientName, (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<DiscordClientOptions>>().Value;
    client.BaseAddress = new Uri($"https+http://{options.ServiceName}/");
});

builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Dota2inHouseLeagueDB")
        ?? throw new InvalidOperationException("Connection string 'Dota2inHouseLeagueDB' is not configured.");
    options.UseSqlServer(connectionString);
});

builder.Services.AddSingleton<IPlayerRepository, PlayerRepository>();
builder.Services.AddSingleton<IMatchRepository, MatchRepository>();
builder.Services.AddSingleton<IChallengeRepository, ChallengeRepository>();
builder.Services.AddHostedService<DatabaseInitializer>();

builder.Services.AddSingleton<DiscordClient>();
builder.Services.AddSingleton<SteamAccountPool>();
builder.Services.AddSingleton<LobbyQueue>();
builder.Services.AddSingleton<SteamGcClientPool>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SteamGcClientPool>());
builder.Services.AddSingleton<LobbyPoolManager>();
builder.Services.AddSingleton<Dota2Service>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LobbyPoolManager>());

var app = builder.Build();

// Crash / hard-shutdown safety net: IHostedService.StopAsync only runs on a
// graceful shutdown, so a SIGTERM-on-host or an unhandled exception would
// otherwise leave the Dota GC holding a ghost lobby. Hook ProcessExit and
// UnhandledException to send PracticeLobbyLeave for every active bot.
{
    var steamPool = app.Services.GetRequiredService<SteamGcClientPool>();
    var lifecycleLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LobbyShutdownHook");

    AppDomain.CurrentDomain.ProcessExit += (_, _) =>
    {
        lifecycleLogger.LogInformation("ProcessExit fired; destroying active lobbies");
        steamPool.DestroyAllActiveLobbies();
    };
    AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    {
        lifecycleLogger.LogError(e.ExceptionObject as Exception, "UnhandledException; destroying active lobbies");
        steamPool.DestroyAllActiveLobbies();
    };
}

app.MapDefaultEndpoints();
app.ConfigureEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Skip HTTPS redirection in Development: the Angular dev server proxies to the
// plain-HTTP endpoint, and a 307 to the HTTPS port would bounce the browser
// cross-origin (different port) and break every admin-panel API call. Production
// still redirects to HTTPS.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.Run();
