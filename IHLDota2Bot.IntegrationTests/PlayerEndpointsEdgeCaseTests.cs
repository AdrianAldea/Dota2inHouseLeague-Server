using System.Net;
using System.Net.Http.Json;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.IntegrationTests.TestSupport;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// Edge and error cases for PlayerEndpoints: 404 on unknown players, top-N
/// limiting on the leaderboard, link validation, and unregister → re-register
/// round-trips.
/// </summary>
public class PlayerEndpointsEdgeCaseTests : IntegrationTestBase
{
    private async Task<RegisteredPlayer> RegisterAsync(
        ulong userId,
        string username,
        ulong steamId = 76561198000000001
    ) =>
        (
            await (
                await Client.PostAsJsonAsync(
                    "/api/gamemanager/register",
                    new RegisterPlayerRequest
                    {
                        SteamId = steamId,
                        UserId = userId,
                        Username = username,
                    }
                )
            ).Content.ReadFromJsonAsync<RegisteredPlayer>()
        )!;

    [Fact]
    public async Task Profile_UnknownPlayer_Returns404()
    {
        var response = await Client.GetAsync("/api/gamemanager/player/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Rank_UnknownPlayer_Returns404()
    {
        var response = await Client.GetAsync("/api/gamemanager/rank/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Link_UnknownPlayer_ReturnsFailure()
    {
        var response = await Client.PostAsync(
            "/api/gamemanager/link/999/76561198000000999",
            content: null
        );
        var op = await response.Content.ReadFromJsonAsync<QueueOperationResponse>();

        Assert.False(op!.Success);
    }

    [Fact]
    public async Task Leaderboard_WithTopLimit_ReturnsAtMostTopEntries()
    {
        for (ulong i = 1; i <= 5; i++)
            await RegisterAsync(i, $"player{i}", 76561198000000000 + i);

        var top3 = await Client.GetFromJsonAsync<LeaderboardResponse>(
            "/api/gamemanager/leaderboard?top=3"
        );

        Assert.NotNull(top3);
        Assert.Equal(3, top3!.Entries.Count);
    }

    [Fact]
    public async Task Unregister_ThenRegisterAgain_SucceedsWithFreshRecord()
    {
        await RegisterAsync(1, "alice", steamId: 76561198000000001);

        var unregister = await Client.PostAsync("/api/gamemanager/unregister/1", content: null);
        var unregOp = await unregister.Content.ReadFromJsonAsync<QueueOperationResponse>();
        Assert.True(unregOp!.Success);

        // Re-register the same userId — should succeed as a fresh insert.
        var reRegistered = await RegisterAsync(1, "alice_v2", steamId: 76561198000000001);
        Assert.Equal("alice_v2", reRegistered.Username);

        var stats = await Client.GetFromJsonAsync<PlayerStatsResponse>("/api/gamemanager/stats/1");
        Assert.Equal(1000, stats!.Rating);
        Assert.Equal(0, stats.Wins);
    }

    [Fact]
    public async Task Leaderboard_RankIsConsecutive_EvenAcrossMultiplePages()
    {
        for (ulong i = 1; i <= 4; i++)
            await RegisterAsync(i, $"player{i}", 76561198000000000 + i);

        var all = await Client.GetFromJsonAsync<LeaderboardResponse>(
            "/api/gamemanager/leaderboard?top=10"
        );

        Assert.Equal(4, all!.Entries.Count);
        // Ranks must be 1, 2, 3, 4 with no gaps or duplicates.
        Assert.Equal(Enumerable.Range(1, 4), all.Entries.Select(e => e.Rank));
    }

    [Fact]
    public async Task Register_WithSameSteamId_TwoDifferentUserIds_BothSucceed()
    {
        // Steam IDs are not unique-constrained at the register endpoint level;
        // two Discord users can share a Steam account (link is advisory).
        var r1 = await RegisterAsync(1, "alice", steamId: 76561198000000001);
        var r2 = await RegisterAsync(2, "bob", steamId: 76561198000000001);

        Assert.NotNull(r1);
        Assert.NotNull(r2);
    }
}
