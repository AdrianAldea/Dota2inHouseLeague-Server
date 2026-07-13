using System.Net;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.Discord.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Moq;

namespace IHLDota2Bot.Discord.Tests;

/// <summary>
/// Tests the bot's HTTP client against the Game Manager API. The HTTP layer is a
/// Moq'd handler, so these verify the real behaviour that matters: the exact
/// endpoint + verb each call uses, query-string building/escaping, and the
/// graceful "return null" handling of 404s and transport failures.
/// </summary>
public class SteamClientTests
{
    private static SteamClient Create(MockHttp http) =>
        new(http.Factory.Object, Mock.Of<ILogger<SteamClient>>());

    [Fact]
    public void Constructor_NullFactory_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new SteamClient(null!, Mock.Of<ILogger<SteamClient>>())
        );

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var http = MockHttp.ReturningStatus(HttpStatusCode.OK);
        Assert.Throws<ArgumentNullException>(() => new SteamClient(http.Factory.Object, null!));
    }

    [Fact]
    public async Task RegisterPlayer_PostsToRegisterEndpoint_AndReturnsPlayer()
    {
        var http = MockHttp.ReturningJson(
            new RegisteredPlayer
            {
                SteamId = "76561",
                Username = "alice",
                UserId = "42",
            }
        );
        var client = Create(http);

        var result = await client.RegisterPlayerAsync(
            new RegisterPlayerRequest
            {
                SteamId = 76561,
                UserId = 42,
                Username = "alice",
            }
        );

        Assert.NotNull(result);
        Assert.Equal("alice", result!.Username);
        var (method, uri) = http.SingleRequest();
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("/api/gamemanager/register", uri.AbsolutePath);
    }

    [Fact]
    public async Task JoinQueue_PostsToUserScopedQueueEndpoint()
    {
        var http = MockHttp.ReturningJson(
            new QueueOperationResponse { Success = true, Message = "joined" }
        );
        var client = Create(http);

        var result = await client.JoinQueueAsync(123);

        Assert.True(result!.Success);
        var (method, uri) = http.SingleRequest();
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("/api/gamemanager/queue/123", uri.AbsolutePath);
    }

    [Fact]
    public async Task GetLeaderboard_BuildsTopQueryString()
    {
        var http = MockHttp.ReturningJson(new LeaderboardResponse { TopN = 15, Entries = [] });
        var client = Create(http);

        var result = await client.GetLeaderboardAsync(15);

        Assert.Equal(15, result!.TopN);
        var (method, uri) = http.SingleRequest();
        Assert.Equal(HttpMethod.Get, method);
        Assert.Equal("/api/gamemanager/leaderboard", uri.AbsolutePath);
        Assert.Equal("?top=15", uri.Query);
    }

    [Fact]
    public async Task ReportMatch_EncodesWinnerInThePath()
    {
        var http = MockHttp.ReturningJson(
            new QueueOperationResponse { Success = true, Message = "ok" }
        );
        var client = Create(http);

        await client.ReportMatchAsync(123, MatchTeam.Radiant);

        var (method, uri) = http.SingleRequest();
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("/api/gamemanager/match/report/123/Radiant", uri.AbsolutePath);
    }

    [Fact]
    public async Task BanPlayer_WithReason_EscapesReasonIntoQueryString()
    {
        var http = MockHttp.ReturningJson(
            new QueueOperationResponse { Success = true, Message = "banned" }
        );
        var client = Create(http);

        await client.BanPlayerAsync(5, "afk farmer");

        var (_, uri) = http.SingleRequest();
        Assert.Equal("/api/gamemanager/admin/ban/5", uri.AbsolutePath);
        Assert.Contains("reason=afk%20farmer", uri.Query);
    }

    [Fact]
    public async Task BanPlayer_WithoutReason_OmitsQueryString()
    {
        var http = MockHttp.ReturningJson(
            new QueueOperationResponse { Success = true, Message = "banned" }
        );
        var client = Create(http);

        await client.BanPlayerAsync(5, reason: null);

        var (_, uri) = http.SingleRequest();
        Assert.Equal("/api/gamemanager/admin/ban/5", uri.AbsolutePath);
        Assert.Equal(string.Empty, uri.Query);
    }

    [Fact]
    public async Task GetPlayerStats_NotFound_ReturnsNull()
    {
        var http = MockHttp.ReturningStatus(HttpStatusCode.NotFound);
        var client = Create(http);

        Assert.Null(await client.GetPlayerStatsAsync(7));
    }

    [Fact]
    public async Task GetPlayerStats_Ok_ReturnsDeserializedStats()
    {
        var http = MockHttp.ReturningJson(
            new PlayerStatsResponse
            {
                UserId = 7,
                Username = "alice",
                SteamId = "steam-7",
                Rating = 1100,
                Wins = 3,
                Losses = 1,
            }
        );
        var client = Create(http);

        var stats = await client.GetPlayerStatsAsync(7);

        Assert.Equal(1100, stats!.Rating);
        Assert.Equal("/api/gamemanager/stats/7", http.SingleRequest().Uri.AbsolutePath);
    }

    [Fact]
    public async Task GetPlayerStats_ServerError_ReturnsNull()
    {
        var http = MockHttp.ReturningStatus(HttpStatusCode.InternalServerError);
        var client = Create(http);

        Assert.Null(await client.GetPlayerStatsAsync(7)); // EnsureSuccessStatusCode throws → swallowed
    }

    [Fact]
    public async Task GetMatchById_NotFound_ReturnsNull()
    {
        var http = MockHttp.ReturningStatus(HttpStatusCode.NotFound);
        var client = Create(http);

        Assert.Null(await client.GetMatchByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task TransportFailure_ReturnsNullInsteadOfThrowing()
    {
        var http = MockHttp.ThrowingTransportError();
        var client = Create(http);

        // Every wrapper swallows exceptions and returns null so a flaky API
        // never takes down the Discord gateway handler.
        Assert.Null(await client.GetLeaderboardAsync(10));
        Assert.Null(await client.JoinQueueAsync(1));
        Assert.Null(await client.GetQueueStatusAsync());
    }
}
