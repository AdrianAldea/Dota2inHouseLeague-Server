using IHLDota2Bot;
using IHLDota2Bot.Clients;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Extensions;
using IHLDota2Bot.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<SteamOptions>(builder.Configuration.GetSection(SteamOptions.SectionName));
builder.Services.Configure<DiscordClientOptions>(builder.Configuration.GetSection(DiscordClientOptions.SectionName));

builder.Services.AddHttpClient(DiscordClient.HttpClientName, (sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DiscordClientOptions>>().Value;
    client.BaseAddress = new Uri($"https+http://{options.ServiceName}/");
});

builder.Services.AddSingleton<DiscordClient>();
builder.Services.AddSingleton<SteamAccountPool>();
builder.Services.AddSingleton<LobbyQueue>();
builder.Services.AddSingleton<LobbyPoolManager>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LobbyPoolManager>());
builder.Services.AddSingleton<Dota2Service>();

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
