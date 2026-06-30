using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IHLDota2Bot;
using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Data.Entities;
using IHLDota2Bot.Data.Repositories;
using IHLDota2Bot.Services;
using IHLDota2Bot.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace IHLDota2Bot.Tests;

/// <summary>
/// Covers the compensating rollback in <see cref="LobbyPoolManager"/> for the window
/// between persisting a match (and reserving a pool account) and the Steam GC actually
/// creating the Dota lobby. When the GC call throws there is no lobby, so the match must
/// be cancelled and the account released — keeping the DB and Steam consistent instead of
/// stranding an InProgress match behind a lobby that never launched.
/// </summary>
public class LobbyPoolManagerLaunchRollbackTests
{
    [Fact]
    public async Task LobbyReady_WhenSteamLobbyCreationThrows_CancelsMatchAndReleasesAccount()
    {
        // A pool client that is logged on but fails to stand up the Dota lobby.
        var gcClient = new Mock<ISteamGcClient>();
        gcClient.SetupGet(c => c.IsLoggedOn).Returns(true);
        gcClient.Setup(c => c.CreateLobbyAsync(It.IsAny<CreateLobbyRequest>()))
            .ThrowsAsync(new InvalidOperationException("GC unavailable"));

        var gcPool = new Mock<ISteamGcClientPool>();
        gcPool.Setup(p => p.GetByUsername(It.IsAny<string>())).Returns(gcClient.Object);

        var match = new MatchEntity { Id = Guid.NewGuid(), LobbyId = "lobby", Status = MatchStatus.InProgress };
        var rollbackSettled = new TaskCompletionSource();
        var repo = new Mock<IMatchRepository>();
        repo.Setup(r => r.GetStaleInProgressMatchesAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MatchEntity>());
        repo.Setup(r => r.CreateMatchAsync(
                It.IsAny<string?>(), It.IsAny<IEnumerable<(ulong, MatchTeam)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(match);
        repo.Setup(r => r.CompleteMatchByLobbyAsync(
                It.IsAny<string>(), It.Is<MatchTeam?>(t => t == null), It.IsAny<CancellationToken>()))
            .Callback(() => rollbackSettled.TrySetResult())
            .ReturnsAsync(new ReportMatchResult { Success = true, Message = "Cancelled" });

        var accountPool = new SteamAccountPool(Mock.Of<ILogger<SteamAccountPool>>());
        await accountPool.RegisterAccountAsync("botacct", "pw");

        var manager = CreateManager(accountPool, gcPool.Object, repo.Object);

        // PlayersPerLobby defaults to 2: two players join and ready up → LobbyReady fires.
        await manager.JoinQueueAsync(1, "alice", "76561198000000001");
        await manager.JoinQueueAsync(2, "bob", "76561198000000002");
        await manager.MarkPlayerReadyAsync(1);
        await manager.MarkPlayerReadyAsync(2);

        // LobbyReady is handled fire-and-forget; wait for the rollback to settle the DB.
        await rollbackSettled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // The match is cancelled exactly once with no winner, players are never invited,
        // the account is released back to the pool, and no session is left behind.
        repo.Verify(r => r.CompleteMatchByLobbyAsync(
            It.IsAny<string>(), It.Is<MatchTeam?>(t => t == null), It.IsAny<CancellationToken>()), Times.Once);
        gcClient.Verify(c => c.InvitePlayersToLobby(It.IsAny<IList<ulong>>()), Times.Never);
        Assert.Equal(1, accountPool.GetAvailableAccountCount());
        Assert.Empty(manager.GetActiveSessions());
    }

    private static LobbyPoolManager CreateManager(
        SteamAccountPool accountPool, ISteamGcClientPool gcPool, IMatchRepository repo)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        var discordClient = new DiscordClient(httpClientFactory.Object, Mock.Of<ILogger<DiscordClient>>());

        return new LobbyPoolManager(
            accountPool,
            new LobbyQueue(Mock.Of<ILogger<LobbyQueue>>(), Options.Create(new LeagueOptions())),
            discordClient,
            repo,
            gcPool,
            new GameMetrics(),
            Options.Create(new SteamOptions()),
            Mock.Of<ILogger<LobbyPoolManager>>());
    }
}
