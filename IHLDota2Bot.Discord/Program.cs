using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.Discord.Extensions;
using IHLDota2Bot.Discord.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddLogging();
builder.Services.AddSingleton(sp => sp.GetRequiredService<ILoggerFactory>().CreateLogger("DefaultLogger"));
builder.Services.AddSingleton<SteamClient>();
builder.Services.AddSingleton<DiscordService>();

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
