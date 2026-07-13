using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using IHLDota2Bot.Auth;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// Verifies the player JWT gate on <c>/api/auth/me</c> and the "act as me" endpoints:
/// the <c>Player</c> policy requires a valid Bearer token regardless of the (anonymous)
/// admin scheme. Legacy id-based reads stay open.
/// </summary>
public class PlayerAuthorizationTests : IAsyncLifetime
{
    private ApiFactory factory = null!;
    private HttpClient client = null!;

    public Task InitializeAsync()
    {
        factory = new ApiFactory();
        client = factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithGarbageToken_Returns401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MeAction_ReadyWithoutToken_Returns401()
    {
        var response = await client.PostAsync("/api/gamemanager/ready", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MeAction_ValidlySignedTokenWithNonNumericSub_Returns401()
    {
        // A correctly signed token whose `sub` isn't a user id must be rejected by
        // the me-group's endpoint filter (401), not fall back to acting as user 0.
        var key = factory.Services.GetRequiredService<PlayerSigningKey>();
        var jwtOptions = factory.Services.GetRequiredService<IOptions<PlayerJwtOptions>>().Value;
        var token = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                issuer: jwtOptions.Issuer,
                audience: jwtOptions.Audience,
                claims: [new Claim(PlayerClaims.Subject, "not-a-number")],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(key.Key, SecurityAlgorithms.HmacSha256)
            )
        );

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/gamemanager/me/stats");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LegacyQueueStatus_RemainsOpen_Returns200()
    {
        // The public, id-free read must stay accessible without any auth.
        var response = await client.GetAsync("/api/gamemanager/queue/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
