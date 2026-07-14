using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using IHLDota2Bot.Auth;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// <c>GET /api/gamemanager/me/queue</c> — the desktop client reconciles its
/// optimistic in-queue flag against this on every poll, so it must reflect the
/// player's real membership/ready state in the current lobby.
/// </summary>
public class MeQueueEndpointTests : IAsyncLifetime
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
    public async Task MeQueue_WithoutToken_Returns401()
    {
        var response = await client.GetAsync("/api/gamemanager/me/queue");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MeQueue_NotQueued_ReturnsNotInQueue()
    {
        var state = await GetMyQueueAsync(userId: 8001);

        Assert.False(state.InQueue);
        Assert.False(state.IsReady);
    }

    [Fact]
    public async Task MeQueue_TracksJoinAndReady()
    {
        const ulong me = 8002;
        await RegisterAsync(me);

        // Join the queue as the signed-in player, then flip ready — the state
        // read must follow each transition.
        Assert.Equal(
            HttpStatusCode.OK,
            (await PostAsMeAsync(me, "/api/gamemanager/queue/join")).StatusCode
        );
        var afterJoin = await GetMyQueueAsync(me);
        Assert.True(afterJoin.InQueue);
        Assert.False(afterJoin.IsReady);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostAsMeAsync(me, "/api/gamemanager/ready")).StatusCode
        );
        var afterReady = await GetMyQueueAsync(me);
        Assert.True(afterReady.InQueue);
        Assert.True(afterReady.IsReady);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostAsMeAsync(me, "/api/gamemanager/unready")).StatusCode
        );
        var afterUnready = await GetMyQueueAsync(me);
        Assert.True(afterUnready.InQueue);
        Assert.False(afterUnready.IsReady);

        Assert.Equal(
            HttpStatusCode.OK,
            (await PostAsMeAsync(me, "/api/gamemanager/queue/leave")).StatusCode
        );
        var afterLeave = await GetMyQueueAsync(me);
        Assert.False(afterLeave.InQueue);
    }

    private async Task RegisterAsync(ulong userId)
    {
        // The service route is open in tests (no AdminApiKey configured = dev mode).
        var response = await client.PostAsJsonAsync(
            "/api/gamemanager/register",
            new
            {
                steamId = 76561198000000000 + userId,
                userId,
                username = $"queueplayer{userId}",
            }
        );
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<MyQueueStateResponse> GetMyQueueAsync(ulong userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/gamemanager/me/queue");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(userId));
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var state = await response.Content.ReadFromJsonAsync<MyQueueStateResponse>();
        Assert.NotNull(state);
        return state;
    }

    private async Task<HttpResponseMessage> PostAsMeAsync(ulong userId, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(userId));
        return await client.SendAsync(request);
    }

    private string TokenFor(ulong userId)
    {
        var key = factory.Services.GetRequiredService<PlayerSigningKey>();
        var jwtOptions = factory.Services.GetRequiredService<IOptions<PlayerJwtOptions>>().Value;
        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                issuer: jwtOptions.Issuer,
                audience: jwtOptions.Audience,
                claims: [new Claim(PlayerClaims.Subject, userId.ToString())],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(key.Key, SecurityAlgorithms.HmacSha256)
            )
        );
    }
}
