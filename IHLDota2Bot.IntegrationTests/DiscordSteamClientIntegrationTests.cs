using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.IntegrationTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// Cross-project integration: the Discord bot's <see cref="SteamClient"/> talking to
/// a live Game Manager API (TestServer). This proves the real HTTP contract between
/// the two services — endpoint shapes, status-code handling, and that the shared
/// Common DTOs serialize on the server and deserialize on the client unchanged.
/// </summary>
public class DiscordSteamClientIntegrationTests : IntegrationTestBase
{
    private SteamClient CreateSteamClient() =>
        new(new SingleClientHttpFactory(Factory.CreateClient()), NullLogger<SteamClient>.Instance);

    [Fact]
    public async Task RegisterPlayer_RoundTripsThroughTheApi()
    {
        var steam = CreateSteamClient();

        var registered = await steam.RegisterPlayerAsync(
            new RegisterPlayerRequest { SteamId = 76561198000000001, UserId = 1, Username = "alice" });

        Assert.NotNull(registered);
        Assert.Equal("alice", registered!.Username);
        Assert.Equal("1", registered.UserId);
    }

    [Fact]
    public async Task GetPlayerStats_AfterRegister_ReturnsDefaultRating()
    {
        var steam = CreateSteamClient();
        await steam.RegisterPlayerAsync(
            new RegisterPlayerRequest { SteamId = 76561198000000001, UserId = 1, Username = "alice" });

        var stats = await steam.GetPlayerStatsAsync(1);

        Assert.NotNull(stats);
        Assert.Equal(1000, stats!.Rating);
        Assert.Equal("alice", stats.Username);
    }

    [Fact]
    public async Task GetPlayerStats_UnknownPlayer_ReturnsNull()
    {
        var steam = CreateSteamClient();

        // The API answers 404; the client maps that to null (not an exception).
        Assert.Null(await steam.GetPlayerStatsAsync(999));
    }

    [Fact]
    public async Task GetLeaderboard_ReflectsRegisteredPlayers()
    {
        var steam = CreateSteamClient();
        await steam.RegisterPlayerAsync(new RegisterPlayerRequest { SteamId = 76561198000000001, UserId = 1, Username = "alice" });
        await steam.RegisterPlayerAsync(new RegisterPlayerRequest { SteamId = 76561198000000002, UserId = 2, Username = "bob" });

        var board = await steam.GetLeaderboardAsync(10);

        Assert.NotNull(board);
        Assert.Equal(2, board!.Entries.Count);
        Assert.Contains(board.Entries, e => e.Username == "alice");
        Assert.Contains(board.Entries, e => e.Username == "bob");
    }

    [Fact]
    public async Task JoinQueue_AfterRegister_Succeeds()
    {
        var steam = CreateSteamClient();
        await steam.RegisterPlayerAsync(new RegisterPlayerRequest { SteamId = 76561198000000001, UserId = 1, Username = "alice" });

        var result = await steam.JoinQueueAsync(1);

        Assert.NotNull(result);
        Assert.True(result!.Success);
    }
}
