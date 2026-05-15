using Dota2Bot.Extensions;
using Dota2Bot.Services;
using SteamKit2;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<SteamService>();

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