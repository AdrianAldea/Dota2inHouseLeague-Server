using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IHLDota2Bot;
using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Data.Entities;
using IHLDota2Bot.Data.Repositories;
using IHLDota2Bot.Services;
using IHLDota2Bot.Telemetry;
using IHLDota2Bot.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace IHLDota2Bot.Tests;

/// <summary>
/// End-to-end through the lobby-ready path: when a lobby fills and readies up,
/// the match persisted by <see cref="LobbyPoolManager"/> must carry rating-balanced
/// teams (via <see cref="TeamBalancer"/> and the DB ratings), not a random split.
/// </summary>
public class LobbyPoolManagerTeamBalanceTests
{
    [Fact]
    public async Task LobbyReady_PersistsRatingBalancedTeams()
    {
        // Ratings chosen so the balanced pairing is unambiguous: greedy must
        // put 1400+900 against 1200+1100 (2300 vs 2300).
        var players = new[]
        {
            new PlayerEntity
            {
                UserId = 1,
                Username = "a",
                SteamId = "76561198000000001",
                Rating = 1400,
            },
            new PlayerEntity
            {
                UserId = 2,
                Username = "b",
                SteamId = "76561198000000002",
                Rating = 1200,
            },
            new PlayerEntity
            {
                UserId = 3,
                Username = "c",
                SteamId = "76561198000000003",
                Rating = 1100,
            },
            new PlayerEntity
            {
                UserId = 4,
                Username = "d",
                SteamId = "76561198000000004",
                Rating = 900,
            },
        };

        var created = new TaskCompletionSource<List<(ulong UserId, MatchTeam Team)>>();
        var repo = new Mock<IMatchRepository>();
        repo.Setup(r =>
                r.GetStaleInProgressMatchesAsync(
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new List<MatchEntity>());
        repo.Setup(r =>
                r.CreateMatchAsync(
                    It.IsAny<string?>(),
                    It.IsAny<IEnumerable<(ulong, MatchTeam)>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string?, IEnumerable<(ulong UserId, MatchTeam Team)>, CancellationToken>(
                (_, teams, _) => created.TrySetResult(teams.ToList())
            )
            .ReturnsAsync(
                new MatchEntity
                {
                    Id = Guid.NewGuid(),
                    LobbyId = "lobby",
                    Status = MatchStatus.InProgress,
                }
            );

        // No Steam client registered for the reserved account: the GC branch is
        // skipped (logged), but the match must still be persisted with balanced teams.
        var gcPool = new Mock<ISteamGcClientPool>();
        gcPool.Setup(p => p.GetByUsername(It.IsAny<string>())).Returns((ISteamGcClient?)null);

        var accountPool = new SteamAccountPool(Mock.Of<ILogger<SteamAccountPool>>());
        await accountPool.RegisterAccountAsync("botacct", "pw");

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        var discordClient = new DiscordClient(
            httpClientFactory.Object,
            Mock.Of<ILogger<DiscordClient>>()
        );

        var manager = new LobbyPoolManager(
            accountPool,
            new LobbyQueue(
                Mock.Of<ILogger<LobbyQueue>>(),
                Options.Create(new LeagueOptions { PlayersPerLobby = 4 })
            ),
            discordClient,
            repo.Object,
            PlayerRepositoryStubs.WithPlayers(players),
            gcPool.Object,
            new GameMetrics(),
            Options.Create(new SteamOptions()),
            Mock.Of<ILogger<LobbyPoolManager>>()
        );

        foreach (var p in players)
        {
            await manager.JoinQueueAsync(p.UserId, p.Username, p.SteamId);
        }
        foreach (var p in players)
        {
            await manager.MarkPlayerReadyAsync(p.UserId);
        }

        // LobbyReady is handled fire-and-forget; wait for the match write.
        var teams = await created.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var radiant = teams
            .Where(t => t.Team == MatchTeam.Radiant)
            .Select(t => t.UserId)
            .ToHashSet();
        var dire = teams.Where(t => t.Team == MatchTeam.Dire).Select(t => t.UserId).ToHashSet();

        Assert.Equal(2, radiant.Count);
        Assert.Equal(2, dire.Count);
        Assert.True(
            radiant.SetEquals([1UL, 4UL]) || dire.SetEquals([1UL, 4UL]),
            "expected the 1400- and 900-rated players to be teammates (2300 vs 2300 split)"
        );
    }
}
