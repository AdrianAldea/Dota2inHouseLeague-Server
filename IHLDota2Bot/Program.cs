using System.Net;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddGameManager(builder.Configuration);

// Behind the Cloudflare Tunnel, cloudflared terminates TLS at the edge and forwards plain
// HTTP from the local loopback, so ctx.Request.Scheme would be "http" and the Steam OpenID
// realm/return_to would come out as http:// even though players connect over https. Honour
// X-Forwarded-Proto so the scheme reflects the public https URL. Trust the header ONLY from
// the loopback cloudflared process (not arbitrary clients) so a remote caller can't spoof it.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Loopback); // 127.0.0.1
    options.KnownProxies.Add(IPAddress.IPv6Loopback); // ::1
});

var app = builder.Build();

// Warn loudly at startup when the admin API key is missing so operators know
// the admin gate is inactive rather than discovering it from a security audit.
var apiKeyOpts = app.Services.GetRequiredService<IOptions<AdminApiKeyOptions>>().Value;
if (string.IsNullOrEmpty(apiKeyOpts.AdminApiKey))
{
    app.Logger.LogWarning(
        "Admin API key is not configured (GameManager:AdminApiKey). "
            + "Admin endpoints are unrestricted. Set the key via user-secrets or "
            + "the GameManager__AdminApiKey environment variable for production."
    );
}

// Warn when no JWT signing key is configured: an ephemeral key is generated so dev
// works, but player tokens won't survive a restart and differ across instances.
var playerJwtOpts = app.Services.GetRequiredService<IOptions<PlayerJwtOptions>>().Value;
if (string.IsNullOrEmpty(playerJwtOpts.SigningKey))
{
    app.Logger.LogWarning(
        "Player JWT signing key is not configured (PlayerJwt:SigningKey). "
            + "Using an ephemeral per-process key — tokens won't survive a restart. "
            + "Set PlayerJwt__SigningKey via user-secrets or environment variable for production."
    );
}

// Apply forwarded headers before anything reads Scheme/Host (Steam OpenID realm/return_to).
app.UseForwardedHeaders();

app.UseLobbyShutdownHooks();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.ConfigureEndpoints();

// Swagger is a Development convenience, but the Development server is sometimes
// exposed publicly (Cloudflare tunnel test sessions — see HOSTING_FOR_TESTING.md),
// where advertising the full API surface is unnecessary. Swagger:Enabled=false
// (e.g. the Swagger__Enabled env var) turns it off without switching environments.
if (app.Environment.IsDevelopment() && app.Configuration.GetValue("Swagger:Enabled", true))
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
