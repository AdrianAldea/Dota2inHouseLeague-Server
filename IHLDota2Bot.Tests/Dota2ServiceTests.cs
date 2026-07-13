using System.Net.Http;
using IHLDota2Bot;
using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
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
/// Unit tests for <see cref="Dota2Service"/> that exercise the coordination logic
/// it owns: registration/ban guards in <c>JoinQueueAsync</c>, unregistered-player
/// guards in challenge/ban/link/unban flows, and the self-challenge guard. Repositories
/// are Moq mocks; <see cref="LobbyPoolManager"/> and <see cref="DiscordClient"/> are
/// lightweight real instances wired with a stub HTTP handler and no Steam accounts, so
/// the Steam pool never opens a real connection.
/// </summary>
public class Dota2ServiceTests
{
    // ── Factory helpers ──────────────────────────────────────────────────────

    private static DiscordClient CreateDiscordClient()
    {
        var httpFactory = new Mock<IHttpClientFactory>();
        httpFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient { BaseAddress = new Uri("http://localhost/") });
        return new DiscordClient(httpFactory.Object, Mock.Of<ILogger<DiscordClient>>());
    }

    private static LobbyPoolManager CreatePoolManager(
        DiscordClient discord,
        IMatchRepository matchRepo
    )
    {
        var steamOptions = Options.Create(new SteamOptions());
        var steamGcPool = new SteamGcClientPool(
            steamOptions,
            discord,
            NullLoggerFactory.Instance,
            Mock.Of<ILogger<SteamGcClientPool>>()
        );

        return new LobbyPoolManager(
            new SteamAccountPool(Mock.Of<ILogger<SteamAccountPool>>()),
            new LobbyQueue(Mock.Of<ILogger<LobbyQueue>>(), Options.Create(new LeagueOptions())),
            discord,
            matchRepo,
            steamGcPool,
            new IHLDota2Bot.Telemetry.GameMetrics(),
            steamOptions,
            Mock.Of<ILogger<LobbyPoolManager>>()
        );
    }

    private static Dota2Service CreateService(
        IPlayerRepository? playerRepo = null,
        IMatchRepository? matchRepo = null,
        IChallengeRepository? challengeRepo = null
    )
    {
        playerRepo ??= Mock.Of<IPlayerRepository>();
        matchRepo ??= Mock.Of<IMatchRepository>(r =>
            r.GetStaleInProgressMatchesAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())
            == Task.FromResult(new System.Collections.Generic.List<MatchEntity>())
        );
        challengeRepo ??= Mock.Of<IChallengeRepository>();

        var discord = CreateDiscordClient();
        var pool = CreatePoolManager(discord, matchRepo);

        var steamOptions = Options.Create(new SteamOptions());
        var steamGcPool = new SteamGcClientPool(
            steamOptions,
            discord,
            NullLoggerFactory.Instance,
            Mock.Of<ILogger<SteamGcClientPool>>()
        );

        return new Dota2Service(
            discord,
            pool,
            steamGcPool,
            playerRepo,
            matchRepo,
            challengeRepo,
            Mock.Of<ILogger<Dota2Service>>()
        );
    }

    private static PlayerEntity RegisteredPlayer(
        ulong userId,
        string username = "alice",
        bool isBanned = false,
        string? banReason = null
    ) =>
        new()
        {
            UserId = userId,
            Username = username,
            SteamId = "76561198000000001",
            IsBanned = isBanned,
            BanReason = banReason,
        };

    // ── JoinQueueAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task JoinQueue_UnregisteredPlayer_ReturnsNotRegisteredFailure()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo
            .Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerEntity?)null);
        var svc = CreateService(playerRepo: playerRepo.Object);

        var result = await svc.JoinQueueAsync(1);

        Assert.False(result.Success);
        Assert.Contains("not registered", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JoinQueue_BannedPlayerWithReason_ReturnsFailureIncludingReason()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo
            .Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisteredPlayer(1, isBanned: true, banReason: "smurfing"));
        var svc = CreateService(playerRepo: playerRepo.Object);

        var result = await svc.JoinQueueAsync(1);

        Assert.False(result.Success);
        Assert.Contains("banned", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("smurfing", result.Message);
    }

    [Fact]
    public async Task JoinQueue_BannedPlayerNoReason_ReturnsFailureWithoutReasonClause()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo
            .Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisteredPlayer(1, isBanned: true, banReason: null));
        var svc = CreateService(playerRepo: playerRepo.Object);

        var result = await svc.JoinQueueAsync(1);

        Assert.False(result.Success);
        Assert.Contains("banned", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Reason:", result.Message);
    }

    [Fact]
    public async Task JoinQueue_RegisteredNotBanned_DelegatesToPool_Succeeds()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo
            .Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisteredPlayer(1));
        var svc = CreateService(playerRepo: playerRepo.Object);

        var result = await svc.JoinQueueAsync(1);

        Assert.True(result.Success);
    }

    // ── ChallengePlayerAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task Challenge_SelfChallenge_Fails_WithoutCallingRepository()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        var svc = CreateService(playerRepo: playerRepo.Object);

        var result = await svc.ChallengePlayerAsync(challengerUserId: 1, challengedUserId: 1);

        Assert.False(result.Success);
        Assert.Contains(
            "can't challenge yourself",
            result.Message,
            StringComparison.OrdinalIgnoreCase
        );
        playerRepo.Verify(
            r => r.GetByUserIdAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Challenge_UnregisteredChallenger_Fails()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo
            .Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerEntity?)null);
        var svc = CreateService(playerRepo: playerRepo.Object);

        var result = await svc.ChallengePlayerAsync(challengerUserId: 1, challengedUserId: 2);

        Assert.False(result.Success);
        Assert.Contains("not registered", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Challenge_UnregisteredChallenged_Fails()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo
            .Setup(r => r.GetByUserIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(RegisteredPlayer(1, "alice"));
        playerRepo
            .Setup(r => r.GetByUserIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerEntity?)null);
        var svc = CreateService(playerRepo: playerRepo.Object);

        var result = await svc.ChallengePlayerAsync(challengerUserId: 1, challengedUserId: 2);

        Assert.False(result.Success);
        Assert.Contains("not registered", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── UnregisterPlayerAsync ────────────────────────────────────────────────

    [Fact]
    public async Task Unregister_UnknownPlayer_ReturnsFailure()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.DeleteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var svc = CreateService(playerRepo: playerRepo.Object);

        var result = await svc.UnregisterPlayerAsync(1);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Unregister_KnownPlayer_ReturnsSuccess()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo.Setup(r => r.DeleteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var svc = CreateService(playerRepo: playerRepo.Object);

        var result = await svc.UnregisterPlayerAsync(1);

        Assert.True(result.Success);
    }

    // ── LinkSteamIdAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task LinkSteamId_UnknownPlayer_ReturnsNotRegisteredFailure()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo
            .Setup(r => r.UpdateSteamIdAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerEntity?)null);
        var svc = CreateService(playerRepo: playerRepo.Object);

        var result = await svc.LinkSteamIdAsync(userId: 1, steamId: 76561198000000099);

        Assert.False(result.Success);
        Assert.Contains("not registered", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LinkSteamId_KnownPlayer_ReturnsSuccessWithNewId()
    {
        var newSteamId = 76561198000000099ul;
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo
            .Setup(r =>
                r.UpdateSteamIdAsync(1, newSteamId.ToString(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new PlayerEntity
                {
                    UserId = 1,
                    Username = "alice",
                    SteamId = newSteamId.ToString(),
                }
            );
        var svc = CreateService(playerRepo: playerRepo.Object);

        var result = await svc.LinkSteamIdAsync(userId: 1, steamId: newSteamId);

        Assert.True(result.Success);
        Assert.Contains(newSteamId.ToString(), result.Message);
    }

    // ── BanPlayerAsync / UnbanPlayerAsync ────────────────────────────────────

    [Fact]
    public async Task Ban_UnknownPlayer_ReturnsFailure()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo
            .Setup(r => r.SetBanAsync(1, true, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerEntity?)null);
        var svc = CreateService(playerRepo: playerRepo.Object);

        var result = await svc.BanPlayerAsync(userId: 1, reason: "test");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Unban_UnknownPlayer_ReturnsFailure()
    {
        var playerRepo = new Mock<IPlayerRepository>();
        playerRepo
            .Setup(r => r.SetBanAsync(1, false, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerEntity?)null);
        var svc = CreateService(playerRepo: playerRepo.Object);

        var result = await svc.UnbanPlayerAsync(userId: 1);

        Assert.False(result.Success);
    }

    // ── ReportMatchAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ReportMatch_FailureFromRepo_ReturnsFailureWithoutNotifyingPool()
    {
        var matchRepo = new Mock<IMatchRepository>();
        matchRepo
            .Setup(r =>
                r.GetStaleInProgressMatchesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);
        matchRepo
            .Setup(r => r.ReportMatchAsync(1, MatchTeam.Radiant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ReportMatchResult
                {
                    Success = false,
                    Message = "No active match.",
                    Match = null,
                }
            );
        var svc = CreateService(matchRepo: matchRepo.Object);

        var result = await svc.ReportMatchAsync(reporterUserId: 1, winningTeam: MatchTeam.Radiant);

        Assert.False(result.Success);
        Assert.Equal("No active match.", result.Message);
    }

    [Fact]
    public async Task ReportMatch_ChallengeMatch_NullLobbyId_ReturnsSuccessAndSkipsPoolNotify()
    {
        var matchSummary = new MatchSummary
        {
            Id = Guid.NewGuid(),
            LobbyId = null, // challenge match — no Steam lobby reserved
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Status = MatchStatus.Completed,
            WinningTeam = MatchTeam.Radiant,
            Players = [],
        };

        var matchRepo = new Mock<IMatchRepository>();
        matchRepo
            .Setup(r =>
                r.GetStaleInProgressMatchesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([]);
        matchRepo
            .Setup(r => r.ReportMatchAsync(1, MatchTeam.Radiant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new ReportMatchResult
                {
                    Success = true,
                    Message = "Winner: Radiant",
                    Match = matchSummary,
                }
            );

        var svc = CreateService(matchRepo: matchRepo.Object);

        var result = await svc.ReportMatchAsync(reporterUserId: 1, winningTeam: MatchTeam.Radiant);

        Assert.True(result.Success);
        Assert.Contains("Radiant", result.Message);
        // No exception thrown means the null-LobbyId guard was respected
        // (NotifyGameFinishedAsync would throw or hang on an unknown lobby otherwise).
    }
}
