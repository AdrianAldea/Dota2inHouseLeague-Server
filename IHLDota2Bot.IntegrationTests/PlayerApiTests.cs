using System.Net;
using System.Net.Http.Json;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.IntegrationTests.TestSupport;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// End-to-end player lifecycle over real HTTP → Dota2Service → EF repositories →
/// in-memory DB: register, read back stats/profile/rank, leaderboard, link, unregister.
/// </summary>
public class PlayerApiTests : IntegrationTestBase
{
    private async Task<RegisteredPlayer> RegisterAsync(
        ulong userId,
        string username,
        ulong steamId = 76561198000000000
    )
    {
        var response = await Client.PostAsJsonAsync(
            "/api/gamemanager/register",
            new RegisterPlayerRequest
            {
                SteamId = steamId,
                UserId = userId,
                Username = username,
            }
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RegisteredPlayer>())!;
    }

    [Fact]
    public async Task Register_PersistsPlayer_WithDefaultRating()
    {
        var registered = await RegisterAsync(1, "alice", steamId: 76561198000000001);

        Assert.Equal("alice", registered.Username);
        Assert.Equal("1", registered.UserId);
        Assert.Equal("76561198000000001", registered.SteamId);

        var stats = await Client.GetFromJsonAsync<PlayerStatsResponse>("/api/gamemanager/stats/1");
        Assert.NotNull(stats);
        Assert.Equal(1000, stats!.Rating);
        Assert.Equal(0, stats.Wins);
        Assert.Equal(0, stats.Losses);
    }

    [Fact]
    public async Task Profile_ReturnsSameDataAsStats()
    {
        await RegisterAsync(1, "alice");

        var profile = await Client.GetFromJsonAsync<PlayerStatsResponse>(
            "/api/gamemanager/player/1"
        );
        var stats = await Client.GetFromJsonAsync<PlayerStatsResponse>("/api/gamemanager/stats/1");

        Assert.Equal(stats!.UserId, profile!.UserId);
        Assert.Equal(stats.Rating, profile.Rating);
        Assert.Equal(stats.Username, profile.Username);
    }

    [Fact]
    public async Task Register_IsUpsert_UpdatesUsername()
    {
        await RegisterAsync(1, "alice");
        await RegisterAsync(1, "alice_renamed");

        var stats = await Client.GetFromJsonAsync<PlayerStatsResponse>("/api/gamemanager/stats/1");
        Assert.Equal("alice_renamed", stats!.Username);

        // Still a single player on the board (upsert, not insert).
        var board = await Client.GetFromJsonAsync<LeaderboardResponse>(
            "/api/gamemanager/leaderboard"
        );
        Assert.Single(board!.Entries);
    }

    [Fact]
    public async Task Stats_UnknownPlayer_Returns404()
    {
        var response = await Client.GetAsync("/api/gamemanager/stats/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Leaderboard_ReturnsRegisteredPlayers_OrderedAndRanked()
    {
        // All start at the default rating, so the tie-break falls through to username asc.
        await RegisterAsync(3, "charlie", steamId: 76561198000000003);
        await RegisterAsync(1, "alice", steamId: 76561198000000001);
        await RegisterAsync(2, "bob", steamId: 76561198000000002);

        var board = await Client.GetFromJsonAsync<LeaderboardResponse>(
            "/api/gamemanager/leaderboard?top=10"
        );

        Assert.Equal(["alice", "bob", "charlie"], board!.Entries.Select(e => e.Username));
        Assert.Equal([1, 2, 3], board.Entries.Select(e => e.Rank));
    }

    [Fact]
    public async Task Rank_SingleRegisteredPlayer_IsFirstWithZeroPercentile()
    {
        await RegisterAsync(1, "alice");

        var rank = await Client.GetFromJsonAsync<RankResponse>("/api/gamemanager/rank/1");

        Assert.Equal(1, rank!.Rank);
        Assert.Equal(1, rank.TotalPlayers);
        Assert.Equal(0d, rank.Percentile);
    }

    [Fact]
    public async Task Link_UpdatesSteamId()
    {
        await RegisterAsync(1, "alice", steamId: 76561198000000001);

        var response = await Client.PostAsync(
            "/api/gamemanager/link/1/76561198000000999",
            content: null
        );
        var op = await response.Content.ReadFromJsonAsync<QueueOperationResponse>();

        Assert.True(op!.Success);
    }

    [Fact]
    public async Task Unregister_RemovesPlayer()
    {
        await RegisterAsync(1, "alice");

        var response = await Client.PostAsync("/api/gamemanager/unregister/1", content: null);
        var op = await response.Content.ReadFromJsonAsync<QueueOperationResponse>();
        Assert.True(op!.Success);

        var afterStats = await Client.GetAsync("/api/gamemanager/stats/1");
        Assert.Equal(HttpStatusCode.NotFound, afterStats.StatusCode);
    }

    [Fact]
    public async Task Unregister_UnknownPlayer_ReturnsFailure()
    {
        var response = await Client.PostAsync("/api/gamemanager/unregister/999", content: null);
        var op = await response.Content.ReadFromJsonAsync<QueueOperationResponse>();

        Assert.False(op!.Success);
    }
}
