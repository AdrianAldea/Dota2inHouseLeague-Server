using IHLDota2Bot.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddGameManager(builder.Configuration);

var app = builder.Build();

app.UseLobbyShutdownHooks();

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
