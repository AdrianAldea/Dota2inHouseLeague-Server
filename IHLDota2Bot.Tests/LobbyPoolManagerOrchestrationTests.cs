using System.Net.Http;
using IHLDota2Bot;
using IHLDota2Bot.Clients;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Data.Repositories;
using IHLDota2Bot.Services;
using IHLDota2Bot.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace IHLDota2Bot.Tests;

/// <summary>
/// Orchestration behavior of <see cref="LobbyPoolManager"/>: the queue join/leave/ready
/// delegation chain, validation guards on <c>AddSteamAccountAsync</c>, and observable
/// queue state. These sit above the pure <see cref="LobbyQueue"/> unit tests — they
/// verify that <see cref="LobbyPoolManager"/> correctly threads operations through to
/// the queue and surfaces results to callers.
/// </summary>
public class LobbyPoolManagerOrchestrationTests
{
    private static LobbyPoolManager CreateManager(IMatchRepository? matchRepository = null)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        var discordClient = new DiscordClient(
            httpClientFactory.Object,
            Mock.Of<ILogger<DiscordClient>>()
        );

        var steamOptions = Options.Create(new SteamOptions());
        var steamGcPool = new SteamGcClientPool(
            steamOptions,
            discordClient,
            NullLoggerFactory.Instance,
            Mock.Of<ILogger<SteamGcClientPool>>()
        );

        var repo =
            matchRepository
            ?? Mock.Of<IMatchRepository>(r =>
                r.GetStaleInProgressMatchesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
                == Task.FromResult(
                    new System.Collections.Generic.List<IHLDota2Bot.Data.Entities.MatchEntity>()
                )
            );

        return new LobbyPoolManager(
            new SteamAccountPool(Mock.Of<ILogger<SteamAccountPool>>()),
            new LobbyQueue(Mock.Of<ILogger<LobbyQueue>>(), Options.Create(new LeagueOptions())),
            discordClient,
            repo,
            PlayerRepositoryStubs.Empty(),
            steamGcPool,
            new IHLDota2Bot.Telemetry.GameMetrics(),
            steamOptions,
            Mock.Of<ILogger<LobbyPoolManager>>()
        );
    }

    // ── Queue join / leave / ready delegation ────────────────────────────────

    [Fact]
    public async Task JoinQueue_NewPlayer_Succeeds()
    {
        var manager = CreateManager();

        var result = await manager.JoinQueueAsync(1, "alice", "76561198000000001");

        Assert.True(result.Success);
    }

    [Fact]
    public async Task JoinQueue_DuplicatePlayer_ReturnsFailure()
    {
        var manager = CreateManager();
        await manager.JoinQueueAsync(1, "alice", "76561198000000001");

        var second = await manager.JoinQueueAsync(1, "alice", "76561198000000001");

        Assert.False(second.Success);
    }

    [Fact]
    public async Task LeaveQueue_AfterJoin_Succeeds()
    {
        var manager = CreateManager();
        await manager.JoinQueueAsync(1, "alice", "76561198000000001");

        var leave = await manager.LeaveQueueAsync(1);

        Assert.True(leave.Success);
    }

    [Fact]
    public async Task LeaveQueue_WhenNotInQueue_ReturnsFailure()
    {
        var manager = CreateManager();

        var leave = await manager.LeaveQueueAsync(999);

        Assert.False(leave.Success);
    }

    [Fact]
    public async Task MarkReady_WhenNotInQueue_ReturnsFailure()
    {
        var manager = CreateManager();

        var ready = await manager.MarkPlayerReadyAsync(999);

        Assert.False(ready.Success);
    }

    [Fact]
    public async Task MarkReady_AfterJoin_Succeeds()
    {
        var manager = CreateManager();
        await manager.JoinQueueAsync(1, "alice", "76561198000000001");

        var ready = await manager.MarkPlayerReadyAsync(1);

        Assert.True(ready.Success);
    }

    [Fact]
    public async Task GetQueueStatus_ReflectsJoinedPlayer()
    {
        var manager = CreateManager();
        await manager.JoinQueueAsync(1, "alice", "76561198000000001");

        var status = manager.GetQueueStatus();

        Assert.Equal(1, status.CurrentLobbyPlayerCount);
        Assert.Contains(status.Players, p => p.Username == "alice");
    }

    [Fact]
    public async Task KickFromLobby_WhenPlayerNotInQueue_ReturnsFailure()
    {
        var manager = CreateManager();

        var kick = await manager.KickFromLobbyAsync(999);

        Assert.False(kick.Success);
    }

    [Fact]
    public async Task ResetCurrentLobby_AfterPlayersJoin_ClearsQueue()
    {
        var manager = CreateManager();
        await manager.JoinQueueAsync(1, "alice", "76561198000000001");
        await manager.JoinQueueAsync(2, "bob", "76561198000000002");

        var reset = await manager.ResetCurrentLobbyAsync();
        Assert.True(reset.Success);

        var status = manager.GetQueueStatus();
        Assert.Equal(0, status.CurrentLobbyPlayerCount);
    }

    // ── AddSteamAccount validation ────────────────────────────────────────────

    [Fact]
    public async Task AddSteamAccount_BlankUsername_ReturnsFailure()
    {
        var manager = CreateManager();

        var result = await manager.AddSteamAccountAsync("", "password");

        Assert.False(result.Success);
        Assert.Contains("required", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddSteamAccount_BlankPassword_ReturnsFailure()
    {
        var manager = CreateManager();

        var result = await manager.AddSteamAccountAsync("username", "");

        Assert.False(result.Success);
        Assert.Contains("required", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddSteamAccount_WhitespaceOnly_ReturnsFailure()
    {
        var manager = CreateManager();

        var result = await manager.AddSteamAccountAsync("   ", "   ");

        Assert.False(result.Success);
    }
}
