using System.Net.Http;
using IHLDota2Bot;
using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Data.Entities;
using IHLDota2Bot.Data.Repositories;
using IHLDota2Bot.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace IHLDota2Bot.Tests;

/// <summary>
/// Tests the orchestrator's startup reconciliation pass — the logic that cancels
/// matches left <c>InProgress</c> by a previous crashed run. <see cref="IMatchRepository"/>
/// is a Moq mock so we can drive each scenario (no stale matches / some stale /
/// repository failure) and verify the resulting interactions precisely.
/// </summary>
public class LobbyPoolManagerTests
{
    private static MatchEntity StaleMatch() =>
        new()
        {
            Id = Guid.NewGuid(),
            Status = MatchStatus.InProgress,
            CreatedAt = DateTime.UtcNow.AddHours(-3),
        };

    private static LobbyPoolManager CreateManager(IMatchRepository matchRepository)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        var discordClient = new DiscordClient(
            httpClientFactory.Object,
            Mock.Of<ILogger<DiscordClient>>()
        );

        var steamOptions = Options.Create(new SteamOptions()); // no configured accounts
        var steamGcPool = new SteamGcClientPool(
            steamOptions,
            discordClient,
            NullLoggerFactory.Instance,
            Mock.Of<ILogger<SteamGcClientPool>>()
        );

        return new LobbyPoolManager(
            new SteamAccountPool(Mock.Of<ILogger<SteamAccountPool>>()),
            new LobbyQueue(Mock.Of<ILogger<LobbyQueue>>(), Options.Create(new LeagueOptions())),
            discordClient,
            matchRepository,
            steamGcPool,
            new IHLDota2Bot.Telemetry.GameMetrics(),
            steamOptions,
            Mock.Of<ILogger<LobbyPoolManager>>()
        );
    }

    [Fact]
    public async Task Start_NoStaleMatches_DoesNotCancelAnything()
    {
        var repo = new Mock<IMatchRepository>();
        repo.Setup(r =>
                r.GetStaleInProgressMatchesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);
        var manager = CreateManager(repo.Object);

        await manager.StartAsync(CancellationToken.None);

        repo.Verify(
            r => r.CancelMatchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Start_WithStaleMatches_CancelsEachOne()
    {
        var stale1 = StaleMatch();
        var stale2 = StaleMatch();
        var repo = new Mock<IMatchRepository>();
        repo.Setup(r =>
                r.GetStaleInProgressMatchesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([stale1, stale2]);
        repo.Setup(r => r.CancelMatchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var manager = CreateManager(repo.Object);

        await manager.StartAsync(CancellationToken.None);

        repo.Verify(r => r.CancelMatchAsync(stale1.Id, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.CancelMatchAsync(stale2.Id, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(
            r => r.CancelMatchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task Start_QueriesUsingTheStaleMatchThreshold()
    {
        var before = DateTime.UtcNow - LeagueConstants.StaleMatchThreshold;
        var repo = new Mock<IMatchRepository>();
        repo.Setup(r =>
                r.GetStaleInProgressMatchesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);
        var manager = CreateManager(repo.Object);

        await manager.StartAsync(CancellationToken.None);
        var after = DateTime.UtcNow - LeagueConstants.StaleMatchThreshold;

        // The cutoff passed to the repo must be "now minus the stale threshold".
        repo.Verify(
            r =>
                r.GetStaleInProgressMatchesAsync(
                    It.Is<DateTime>(cutoff => cutoff >= before && cutoff <= after),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Start_WhenReconciliationThrows_StartupStillCompletes()
    {
        var repo = new Mock<IMatchRepository>();
        repo.Setup(r =>
                r.GetStaleInProgressMatchesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("DB unavailable on boot"));
        var manager = CreateManager(repo.Object);

        // Reconciliation is best-effort: a DB failure must not bubble out of StartAsync.
        var ex = await Record.ExceptionAsync(() => manager.StartAsync(CancellationToken.None));

        Assert.Null(ex);
        repo.Verify(
            r => r.CancelMatchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
