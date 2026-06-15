using Discord;
using Discord.WebSocket;
using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.Discord.Configuration;
using IHLDota2Bot.Discord.Extensions;
using IHLDota2Bot.Discord.Services;
using Microsoft.Extensions.Options;

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
