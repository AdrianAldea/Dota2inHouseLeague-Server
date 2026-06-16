using Discord;
using Discord.WebSocket;
using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.Discord.Commands;
using IHLDota2Bot.Discord.Configuration;
using IHLDota2Bot.Discord.Extensions;
using IHLDota2Bot.Discord.Services;
using IHLDota2Bot.Discord.Telemetry;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<DiscordOptions>(builder.Configuration.GetSection(DiscordOptions.SectionName));
builder.Services.Configure<SteamClientOptions>(builder.Configuration.GetSection(SteamClientOptions.SectionName));

builder.Services.AddHttpClient(SteamClient.HttpClientName, (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<SteamClientOptions>>().Value;
    client.BaseAddress = new Uri($"https+http://{options.ServiceName}/");
});

builder.Services.AddSingleton<ISteamClient, SteamClient>();

// The Discord gateway client is owned by DI so DiscordService can be constructed
// (and unit-tested) with an injected socket client rather than newing one up.
builder.Services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
}));

// Command handlers — each handles exactly one DiscordAction.
// Registered as IDiscordCommandHandler so DiscordCommandDispatcher can collect them all.
// Info / read-only queries
builder.Services.AddSingleton<IDiscordCommandHandler, PingCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, VersionCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, CommandsCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, QueueStatusCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, ProfileCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, MyStatsCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, StatsCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, LeaderboardCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, RankCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, CurrentMatchCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, RecentMatchesCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, MatchScoreCommandHandler>();
// Registration
builder.Services.AddSingleton<IDiscordCommandHandler, RegisterCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, LinkCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, UnregisterCommandHandler>();
// Queue / lobby
builder.Services.AddSingleton<IDiscordCommandHandler, JoinQueueCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, LeaveQueueCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, ReadyCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, UnreadyCommandHandler>();
// Match actions
builder.Services.AddSingleton<IDiscordCommandHandler, ReportMatchCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, ChallengeCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, AcceptChallengeCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, DeclineChallengeCommandHandler>();
// Admin
builder.Services.AddSingleton<IDiscordCommandHandler, BanCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, UnbanCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, KickCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, ForceStartCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, ResetQueueCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, AddAccountCommandHandler>();
builder.Services.AddSingleton<IDiscordCommandHandler, PoolStatusCommandHandler>();
builder.Services.AddSingleton<DiscordCommandDispatcher>();

// Command-pipeline cross-cutting concerns: throttling + telemetry.
builder.Services.Configure<CommandRateLimitOptions>(
    builder.Configuration.GetSection(CommandRateLimitOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CommandRateLimiter>();
builder.Services.AddSingleton<DiscordCommandTelemetry>();

// Surface the command meter + activity source through the existing OpenTelemetry pipeline.
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter(DiscordCommandTelemetry.MeterName))
    .WithTracing(tracing => tracing.AddSource(DiscordCommandTelemetry.ActivitySourceName));

builder.Services.AddSingleton<DiscordService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DiscordService>());

var app = builder.Build();

app.MapDefaultEndpoints();
app.ConfigureEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Run();
