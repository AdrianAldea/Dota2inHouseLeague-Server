using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using IHLDota2Bot.Auth;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Data.Repositories;
using IHLDota2Bot.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// <c>GET /api/gamemanager/me/match</c> — the desktop client's "match found"
/// screen: the signed-in player's current match plus the Dota lobby credentials
/// (derived from the lobby id) as a fallback for a missed Steam invite.
/// </summary>
public class MeMatchEndpointTests : IAsyncLifetime
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
    public async Task MeMatch_WithoutToken_Returns401()
    {
        var response = await client.GetAsync("/api/gamemanager/me/match");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MeMatch_NoActiveMatch_Returns404()
    {
        var response = await SendAsMeAsync(userId: 7001);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MeMatch_ActiveMatch_ReturnsTeamsAndLobbyCredentials()
    {
        const ulong me = 7002;
        const string lobbyId = "abcdef1234567890";
        var matches = factory.Services.GetRequiredService<IMatchRepository>();
        await matches.CreateMatchAsync(
            lobbyId,
            [(me, MatchTeam.Radiant), (7003UL, MatchTeam.Dire)]
        );

        var response = await SendAsMeAsync(me);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CurrentMatchResponse>();
        Assert.NotNull(body);
        Assert.Equal(lobbyId, body.Match.LobbyId);
        Assert.Equal(MatchStatus.InProgress, body.Match.Status);
        Assert.Contains(body.Match.Players, p => p.UserId == me && p.Team == MatchTeam.Radiant);
        // Credentials derive from the first 8 chars of the lobby id (LobbyNaming).
        Assert.Equal("Dota2IHL-abcdef12", body.LobbyName);
        Assert.Equal(body.LobbyName, body.LobbyPassword);
    }

    [Fact]
    public async Task MeMatch_MatchWithoutLobby_ReturnsNullCredentials()
    {
        const ulong me = 7004;
        var matches = factory.Services.GetRequiredService<IMatchRepository>();
        await matches.CreateMatchAsync(lobbyId: null, [(me, MatchTeam.Dire)]);

        var response = await SendAsMeAsync(me);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CurrentMatchResponse>();
        Assert.NotNull(body);
        Assert.Null(body.LobbyName);
        Assert.Null(body.LobbyPassword);
    }

    private async Task<HttpResponseMessage> SendAsMeAsync(ulong userId)
    {
        var key = factory.Services.GetRequiredService<PlayerSigningKey>();
        var jwtOptions = factory.Services.GetRequiredService<IOptions<PlayerJwtOptions>>().Value;
        var token = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                issuer: jwtOptions.Issuer,
                audience: jwtOptions.Audience,
                claims: [new Claim(PlayerClaims.Subject, userId.ToString())],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(key.Key, SecurityAlgorithms.HmacSha256)
            )
        );

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/gamemanager/me/match");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
