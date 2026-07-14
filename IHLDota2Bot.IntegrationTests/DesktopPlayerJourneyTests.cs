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
/// End-to-end walkthroughs of the desktop-client player journey against the real
/// API pipeline (real endpoints, Dota2Service, LobbyQueue, LobbyPoolManager,
/// repositories; only SQL Server, Discord, and the Steam GC are stubbed out).
/// Each test is one real situation a player can hit between "I installed the
/// client" and "my match is on the leaderboard". Tokens are minted exactly the
/// way the Steam callback / register endpoint mint them, so the requests are
/// byte-for-byte what the desktop ApiClient sends.
///
/// The default test config runs with League:PlayersPerLobby = 2, so a "full
/// lobby" here is two players.
/// </summary>
public class DesktopPlayerJourneyTests : IAsyncLifetime
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

    // ── Scenario 1: brand-new Steam user registers, plays, and gets rated ──

    [Fact]
    public async Task NewSteamUser_FullJourney_RegisterQueueReadyMatchReport()
    {
        // A Steam bot account exists in the pool (bookkeeping only — no GC client
        // in tests, which matches "bot configured but Steam offline"; the match
        // still gets created and the account reserved).
        await SeedBotAccountAsync("journey-bot-1");

        const ulong alice = 76561198100000001;
        const ulong bob = 76561198100000002;

        // 1. Fresh Steam sign-in: the pre-registration token knows the SteamID
        //    but /auth/me says "not registered yet" → client shows the
        //    pick-a-name screen.
        var aliceMe = await GetAsync<MeResponse>(SteamToken(alice), "/api/auth/me");
        Assert.False(aliceMe.Registered);
        Assert.Equal(alice.ToString(), aliceMe.SteamId);

        // 2. Both players register through the desktop flow.
        var aliceAuth = await RegisterViaDesktopAsync(alice, "Alice");
        var bobAuth = await RegisterViaDesktopAsync(bob, "Bob");
        Assert.True(aliceAuth.Registered);
        Assert.Equal("Alice", aliceAuth.Profile?.Username);

        // 3. The fresh token now reports a registered identity (auto-login path).
        var aliceMe2 = await GetAsync<MeResponse>(aliceAuth.Token, "/api/auth/me");
        Assert.True(aliceMe2.Registered);

        // 4. Both join the queue; /me/queue and the public status agree.
        Assert.True((await PostOpAsync(aliceAuth.Token, "/api/gamemanager/queue/join")).Success);
        Assert.True((await PostOpAsync(bobAuth.Token, "/api/gamemanager/queue/join")).Success);

        var aliceQueue = await GetAsync<MyQueueStateResponse>(
            aliceAuth.Token,
            "/api/gamemanager/me/queue"
        );
        Assert.True(aliceQueue.InQueue);
        Assert.False(aliceQueue.IsReady);

        var status = await GetAsync<QueueStatusResponse>(
            aliceAuth.Token,
            "/api/gamemanager/queue/status"
        );
        Assert.Equal(2, status.CurrentLobbyPlayerCount);

        // 5. Both ready up → the lobby fires and a match is persisted (the
        //    launch handler runs in the background, so poll /me/match).
        Assert.True((await PostOpAsync(aliceAuth.Token, "/api/gamemanager/ready")).Success);
        Assert.True((await PostOpAsync(bobAuth.Token, "/api/gamemanager/ready")).Success);

        var aliceMatch = await WaitForMyMatchAsync(aliceAuth.Token);
        var bobMatch = await WaitForMyMatchAsync(bobAuth.Token);

        // 6. The match-found card has everything the players need: they're on
        //    opposite sides, and the lobby credentials are present and derived
        //    from the lobby id.
        Assert.Equal(aliceMatch.Match.Id, bobMatch.Match.Id);
        Assert.Equal(2, aliceMatch.Match.Players.Count);
        var aliceSide = aliceMatch.Match.Players.Single(p => p.UserId == alice).Team;
        var bobSide = aliceMatch.Match.Players.Single(p => p.UserId == bob).Team;
        Assert.NotEqual(aliceSide, bobSide);
        Assert.NotNull(aliceMatch.LobbyName);
        Assert.Equal(aliceMatch.LobbyName, aliceMatch.LobbyPassword);
        Assert.StartsWith("Dota2IHL-", aliceMatch.LobbyName);

        // 7. The reserved bot account is in use while the match runs.
        var pool = await GetAsync<PoolStatusResponse>(
            aliceAuth.Token,
            "/api/gamemanager/admin/pool_status"
        );
        Assert.Equal(1, pool.InUse);
        Assert.Equal(0, pool.Ready);

        // 8. The game ends; the result is reported for Alice's side (in
        //    production the GC does this automatically; the service route is the
        //    same code path).
        var report = await PostAsync(
            $"/api/gamemanager/match/report/{alice}/{aliceSide.ToString().ToLowerInvariant()}"
        );
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);

        // 9. The loop is closed: no current match, ELO applied (winner above
        //    the default 1000 rating, loser below), W/L recorded, and the bot
        //    account released for the next game.
        var gone = await GetRawAsync(aliceAuth.Token, "/api/gamemanager/me/match");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

        var aliceStats = await GetAsync<PlayerStatsResponse>(
            aliceAuth.Token,
            "/api/gamemanager/me/stats"
        );
        var bobStats = await GetAsync<PlayerStatsResponse>(
            bobAuth.Token,
            "/api/gamemanager/me/stats"
        );
        Assert.Equal(1, aliceStats.Wins);
        Assert.Equal(0, aliceStats.Losses);
        Assert.Equal(1, bobStats.Losses);
        Assert.True(aliceStats.Rating > bobStats.Rating);

        var poolAfter = await GetAsync<PoolStatusResponse>(
            aliceAuth.Token,
            "/api/gamemanager/admin/pool_status"
        );
        Assert.Equal(0, poolAfter.InUse);

        // 10. Queue rollover behavior (pinned deliberately): finishing the lobby
        //     auto-seats the waiting players into the fresh lobby — Alice and Bob
        //     are back in the queue, unready, without clicking FIND MATCH.
        var aliceRequeued = await GetAsync<MyQueueStateResponse>(
            aliceAuth.Token,
            "/api/gamemanager/me/queue"
        );
        Assert.True(aliceRequeued.InQueue);
        Assert.False(aliceRequeued.IsReady);
    }

    // ── Scenario 2: registration edge cases ──

    [Fact]
    public async Task Register_BlankUsername_GetsGeneratedFallbackName()
    {
        const ulong steamId = 76561198100000011;

        var auth = await RegisterViaDesktopAsync(steamId, "   ");

        // AuthEndpoints falls back to "Player" + the SteamID's last 4 digits.
        Assert.Equal($"Player{steamId.ToString()[^4..]}", auth.Profile?.Username);
    }

    [Fact]
    public async Task Register_Twice_UpdatesNameAndKeepsTheAccount()
    {
        const ulong steamId = 76561198100000012;

        await RegisterViaDesktopAsync(steamId, "FirstName");
        var second = await RegisterViaDesktopAsync(steamId, "SecondName");

        Assert.True(second.Registered);
        Assert.Equal("SecondName", second.Profile?.Username);
        Assert.Equal(LeagueConstants.DefaultRating, second.Profile?.Rating);
    }

    [Fact]
    public async Task UnregisteredPlayer_JoinQueue_IsRefusedWithGuidance()
    {
        // Signed in through Steam but never completed registration.
        var result = await PostOpAsync(
            SteamToken(76561198100000013),
            "/api/gamemanager/queue/join"
        );

        Assert.False(result.Success);
        Assert.Contains("not registered", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Scenario 3: queue misuse a real player will hit ──

    [Fact]
    public async Task JoinTwice_SecondAttemptRefused_PlayerStaysQueued()
    {
        var auth = await RegisterViaDesktopAsync(76561198100000021, "DoubleClicker");

        Assert.True((await PostOpAsync(auth.Token, "/api/gamemanager/queue/join")).Success);
        var second = await PostOpAsync(auth.Token, "/api/gamemanager/queue/join");

        Assert.False(second.Success);
        Assert.Contains("already in lobby", second.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            (await GetAsync<MyQueueStateResponse>(auth.Token, "/api/gamemanager/me/queue")).InQueue
        );
    }

    [Fact]
    public async Task ReadyOrLeave_WithoutBeingQueued_AreRefused()
    {
        var auth = await RegisterViaDesktopAsync(76561198100000022, "Impatient");

        var ready = await PostOpAsync(auth.Token, "/api/gamemanager/ready");
        Assert.False(ready.Success);
        Assert.Contains("not in a lobby", ready.Message, StringComparison.OrdinalIgnoreCase);

        var leave = await PostOpAsync(auth.Token, "/api/gamemanager/queue/leave");
        Assert.False(leave.Success);
        Assert.Contains("not in the queue", leave.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LeaveAndRejoin_ClearsReadyState()
    {
        var auth = await RegisterViaDesktopAsync(76561198100000023, "SecondThoughts");

        Assert.True((await PostOpAsync(auth.Token, "/api/gamemanager/queue/join")).Success);
        Assert.True((await PostOpAsync(auth.Token, "/api/gamemanager/ready")).Success);
        Assert.True((await PostOpAsync(auth.Token, "/api/gamemanager/queue/leave")).Success);

        var afterLeave = await GetAsync<MyQueueStateResponse>(
            auth.Token,
            "/api/gamemanager/me/queue"
        );
        Assert.False(afterLeave.InQueue);

        Assert.True((await PostOpAsync(auth.Token, "/api/gamemanager/queue/join")).Success);
        var rejoined = await GetAsync<MyQueueStateResponse>(
            auth.Token,
            "/api/gamemanager/me/queue"
        );
        Assert.True(rejoined.InQueue);
        Assert.False(rejoined.IsReady);
    }

    [Fact]
    public async Task BannedPlayer_JoinQueue_SeesTheBanReason()
    {
        const ulong steamId = 76561198100000024;
        var auth = await RegisterViaDesktopAsync(steamId, "Troublemaker");

        var ban = await PostAsync($"/api/gamemanager/admin/ban/{steamId}?reason=flaming");
        Assert.Equal(HttpStatusCode.OK, ban.StatusCode);

        var join = await PostOpAsync(auth.Token, "/api/gamemanager/queue/join");
        Assert.False(join.Success);
        Assert.Contains("banned", join.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("flaming", join.Message);

        // Unban restores access.
        Assert.Equal(
            HttpStatusCode.OK,
            (await PostAsync($"/api/gamemanager/admin/unban/{steamId}")).StatusCode
        );
        Assert.True((await PostOpAsync(auth.Token, "/api/gamemanager/queue/join")).Success);
    }

    [Fact]
    public async Task LobbyFull_ThirdPlayerIsRefused_EvenWhileTheMatchRuns()
    {
        await SeedBotAccountAsync("journey-bot-2");
        var a = await RegisterViaDesktopAsync(76561198100000031, "PlayerA");
        var b = await RegisterViaDesktopAsync(76561198100000032, "PlayerB");
        var c = await RegisterViaDesktopAsync(76561198100000033, "PlayerC");

        Assert.True((await PostOpAsync(a.Token, "/api/gamemanager/queue/join")).Success);
        Assert.True((await PostOpAsync(b.Token, "/api/gamemanager/queue/join")).Success);

        // Lobby is full (2/2) — C can't get a seat…
        var whileFull = await PostOpAsync(c.Token, "/api/gamemanager/queue/join");
        Assert.False(whileFull.Success);
        Assert.Contains("full", whileFull.Message, StringComparison.OrdinalIgnoreCase);

        // …and the seat doesn't open up while A and B's match is running either:
        // the launched lobby stays the current one until the game finishes.
        Assert.True((await PostOpAsync(a.Token, "/api/gamemanager/ready")).Success);
        Assert.True((await PostOpAsync(b.Token, "/api/gamemanager/ready")).Success);
        await WaitForMyMatchAsync(a.Token);

        var duringMatch = await PostOpAsync(c.Token, "/api/gamemanager/queue/join");
        Assert.False(duringMatch.Success);
    }

    // ── Scenario 4: every player readies up but no bot account is free ──

    [Fact]
    public async Task NoBotAccount_AllReady_NoMatch_AndReadyCycleRetriesTheLaunch()
    {
        // Deliberately NO seeded pool account: the lobby fires, the launch
        // handler finds no account, and no match is created.
        var a = await RegisterViaDesktopAsync(76561198100000041, "Hopeful");
        var b = await RegisterViaDesktopAsync(76561198100000042, "Patient");

        Assert.True((await PostOpAsync(a.Token, "/api/gamemanager/queue/join")).Success);
        Assert.True((await PostOpAsync(b.Token, "/api/gamemanager/queue/join")).Success);
        Assert.True((await PostOpAsync(a.Token, "/api/gamemanager/ready")).Success);
        Assert.True((await PostOpAsync(b.Token, "/api/gamemanager/ready")).Success);

        await Task.Delay(500); // give the fire-and-forget launch handler time to run
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await GetRawAsync(a.Token, "/api/gamemanager/me/match")).StatusCode
        );

        // Recovery without an admin: once an account exists, cycling one
        // player's ready state re-fires the all-ready launch.
        await SeedBotAccountAsync("late-bot");
        Assert.True((await PostOpAsync(a.Token, "/api/gamemanager/unready")).Success);
        Assert.True((await PostOpAsync(a.Token, "/api/gamemanager/ready")).Success);

        var match = await WaitForMyMatchAsync(a.Token);
        Assert.Equal(2, match.Match.Players.Count);
    }

    // ── helpers ──

    /// <summary>Registers a pool bot account (bookkeeping only, no Steam connection).</summary>
    private Task SeedBotAccountAsync(string username) =>
        factory
            .Services.GetRequiredService<SteamAccountPool>()
            .RegisterAccountAsync(username, "test-password");

    /// <summary>The pre-registration token the Steam callback issues (sub = steamId).</summary>
    private string SteamToken(ulong steamId64)
    {
        var key = factory.Services.GetRequiredService<PlayerSigningKey>();
        var jwtOptions = factory.Services.GetRequiredService<IOptions<PlayerJwtOptions>>().Value;
        return new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(
                issuer: jwtOptions.Issuer,
                audience: jwtOptions.Audience,
                claims:
                [
                    new Claim(PlayerClaims.Subject, steamId64.ToString()),
                    new Claim(PlayerClaims.SteamId, steamId64.ToString()),
                    new Claim(PlayerClaims.Registered, "false"),
                ],
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(key.Key, SecurityAlgorithms.HmacSha256)
            )
        );
    }

    /// <summary>The exact desktop registration call: POST /api/auth/register with the Steam token.</summary>
    private async Task<AuthResultResponse> RegisterViaDesktopAsync(ulong steamId64, string username)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            SteamToken(steamId64)
        );
        request.Content = JsonContent.Create(new { username });

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResultResponse>();
        Assert.NotNull(auth);
        return auth;
    }

    private async Task<QueueOperationResponse> PostOpAsync(string token, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var op = await response.Content.ReadFromJsonAsync<QueueOperationResponse>();
        Assert.NotNull(op);
        return op;
    }

    private Task<HttpResponseMessage> PostAsync(string route) =>
        client.PostAsync(route, content: null);

    private async Task<T> GetAsync<T>(string token, string route)
    {
        var response = await GetRawAsync(token, route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(body);
        return body;
    }

    private async Task<HttpResponseMessage> GetRawAsync(string token, string route)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    /// <summary>
    /// The lobby-ready → match-created hop runs on a background handler, exactly
    /// like production — poll /me/match the way the desktop client's 2s loop does.
    /// </summary>
    private async Task<CurrentMatchResponse> WaitForMyMatchAsync(string token)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var response = await GetRawAsync(token, "/api/gamemanager/me/match");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var match = await response.Content.ReadFromJsonAsync<CurrentMatchResponse>();
                Assert.NotNull(match);
                return match;
            }
            await Task.Delay(100);
        }

        Assert.Fail("Timed out waiting for /me/match to report the created match.");
        return null!; // unreachable
    }
}
