using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using IHLDota2Bot.Auth;
using IHLDota2Bot.Clients;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Data;
using IHLDota2Bot.IntegrationTests.TestSupport;
using IHLDota2Bot.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// Verifies the service-tier gate: every id-based mutation route (acting on an
/// arbitrary player by id) requires the <c>X-Api-Key</c>, while the public reads
/// and the JWT me-endpoints are unaffected by it. This is what makes the API safe
/// to expose to players' desktop clients in production — without the gate, any
/// player could queue/ready/unregister/report as anyone else by guessing an id.
/// </summary>
public class ServiceAuthorizationTests : IAsyncLifetime
{
    private const string CorrectKey = "test-service-key-xyz789";

    private ConfigurableApiFactory factory = null!;
    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        factory = new ConfigurableApiFactory(
            new Dictionary<string, string?> { ["GameManager:AdminApiKey"] = CorrectKey }
        );
        client = factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    public static TheoryData<string> ServiceRoutes =>
        new(
            "/api/gamemanager/queue/1",
            "/api/gamemanager/leave/1",
            "/api/gamemanager/ready/1",
            "/api/gamemanager/unready/1",
            "/api/gamemanager/unregister/1",
            "/api/gamemanager/link/1/2",
            "/api/gamemanager/match/report/1/radiant",
            "/api/gamemanager/challenge/1/2",
            "/api/gamemanager/challenge/accept/1",
            "/api/gamemanager/challenge/decline/1",
            "/api/server/start"
        );

    [Theory]
    [MemberData(nameof(ServiceRoutes))]
    public async Task ServiceRoute_WithoutKey_Returns401(string route)
    {
        var response = await client.PostAsync(route, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(ServiceRoutes))]
    public async Task ServiceRoute_WithKey_IsNotRejectedByAuth(string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route);
        request.Headers.Add(AdminApiKeyAuthenticationHandler.HeaderName, CorrectKey);

        var response = await client.SendAsync(request);

        // Domain-level failures (unknown player, no bots, ...) are fine here —
        // the assertion is purely that the gate lets a keyed caller through.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithoutKey_Returns401()
    {
        var response = await client.PostAsJsonAsync(
            "/api/gamemanager/register",
            new
            {
                steamId = 76561198000000001,
                userId = 1,
                username = "smoketest",
            }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithKey_Succeeds()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/gamemanager/register");
        request.Headers.Add(AdminApiKeyAuthenticationHandler.HeaderName, CorrectKey);
        request.Content = JsonContent.Create(
            new
            {
                steamId = 76561198000000002,
                userId = 2,
                username = "keyedcaller",
            }
        );

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/gamemanager/queue/status")]
    [InlineData("/api/gamemanager/leaderboard")]
    public async Task PublicRead_WithoutKey_Returns200(string route)
    {
        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/gamemanager/stats/999")]
    [InlineData("/api/gamemanager/player/999")]
    [InlineData("/api/gamemanager/rank/999")]
    [InlineData("/api/gamemanager/match/current/999")]
    public async Task PublicRead_WithoutKey_IsNotGated(string route)
    {
        // Unknown ids yield 404s — the point is they must not be 401s.
        var response = await client.GetAsync(route);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MeEndpoint_WithPlayerJwtAndNoApiKey_IsNotGatedByServiceKey()
    {
        // Players never hold the service key; their JWT alone must keep working
        // even when the service gate is active.
        var key = factory.Services.GetRequiredService<PlayerSigningKey>();
        var jwtOptions = factory.Services.GetRequiredService<IOptions<PlayerJwtOptions>>().Value;
        var token = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                issuer: jwtOptions.Issuer,
                audience: jwtOptions.Audience,
                claims: [new Claim(PlayerClaims.Subject, "42")],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(key.Key, SecurityAlgorithms.HmacSha256)
            )
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/gamemanager/queue/join");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

/// <summary>
/// <see cref="ApiFactory"/> variant taking arbitrary configuration overrides
/// (admin key, rate limits, ...) for tests that exercise config-gated behavior.
/// </summary>
public sealed class ConfigurableApiFactory(Dictionary<string, string?> settings)
    : WebApplicationFactory<Dota2Service>
{
    private readonly InMemoryDatabaseRoot dbRoot = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<SteamOptions>(options =>
            {
                options.BotAccount = new SteamAccountCredentials();
                options.PoolAccounts = [];
            });

            var efDescriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions)
                    || (
                        d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericArguments().Contains(typeof(AppDbContext))
                    )
                )
                .ToList();
            foreach (var d in efDescriptors)
                services.Remove(d);

            services.AddDbContextFactory<AppDbContext>(o =>
                o.UseInMemoryDatabase($"ihl-itests-{Guid.NewGuid():N}", dbRoot)
            );

            services
                .AddHttpClient(
                    DiscordClient.HttpClientName,
                    c => c.BaseAddress = new Uri("http://localhost/")
                )
                .ConfigurePrimaryHttpMessageHandler(() => new StubOkHandler());
        });

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
    }
}
