using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Data;
using IHLDota2Bot.Data.Entities;
using IHLDota2Bot.Data.Repositories;
using IHLDota2Bot.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace IHLDota2Bot.Tests;

/// <summary>
/// End-to-end repository behaviour against a real EF Core in-memory store. These
/// cover the production-critical paths: creating a match, settling it (which runs
/// the real <see cref="EloCalculator"/> and applies W/L + rating), cancelling,
/// idempotency, and the startup stale-match query.
/// </summary>
public class MatchRepositoryTests
{
    private static PlayerEntity Player(ulong id, int rating = 1000) =>
        new()
        {
            UserId = id,
            Username = $"player{id}",
            SteamId = $"steam-{id}",
            Rating = rating,
            RegisteredAt = DateTime.UtcNow,
        };

    private static async Task SeedPlayersAsync(
        Mock<IDbContextFactory<AppDbContext>> factory,
        params PlayerEntity[] players
    )
    {
        await using var ctx = await factory.NewContextAsync();
        ctx.Players.AddRange(players);
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateMatch_PersistsInProgressMatchWithTeams()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new MatchRepository(factory.Object);

        var match = await repo.CreateMatchAsync(
            "lobby-1",
            [(1UL, MatchTeam.Radiant), (2UL, MatchTeam.Dire)]
        );

        Assert.NotEqual(Guid.Empty, match.Id);

        var fetched = await repo.GetByIdAsync(match.Id);
        Assert.NotNull(fetched);
        Assert.Equal(MatchStatus.InProgress, fetched!.Status);
        Assert.Equal("lobby-1", fetched.LobbyId);
        Assert.Equal(2, fetched.Players.Count);
    }

    [Fact]
    public async Task GetByLobbyId_ReturnsMostRecentMatchForLobby()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new MatchRepository(factory.Object);
        await repo.CreateMatchAsync("lobby-1", [(1UL, MatchTeam.Radiant)]);

        var found = await repo.GetByLobbyIdAsync("lobby-1");

        Assert.NotNull(found);
        Assert.Equal("lobby-1", found!.LobbyId);
        Assert.Null(await repo.GetByLobbyIdAsync("other-lobby"));
    }

    [Fact]
    public async Task GetCurrentMatchForPlayer_ReturnsActiveMatch()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new MatchRepository(factory.Object);
        await repo.CreateMatchAsync("lobby-1", [(1UL, MatchTeam.Radiant), (2UL, MatchTeam.Dire)]);

        var current = await repo.GetCurrentMatchForPlayerAsync(1);

        Assert.NotNull(current);
        Assert.Equal(MatchStatus.InProgress, current!.Status);
        Assert.Null(await repo.GetCurrentMatchForPlayerAsync(999)); // not in any match
    }

    [Fact]
    public async Task ReportMatch_AppliesEloWinsAndLosses_ToParticipants()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new MatchRepository(factory.Object);
        await SeedPlayersAsync(factory, Player(1, rating: 1000), Player(2, rating: 1000));
        await repo.CreateMatchAsync("lobby-1", [(1UL, MatchTeam.Radiant), (2UL, MatchTeam.Dire)]);

        var result = await repo.ReportMatchAsync(reporterUserId: 1, winningTeam: MatchTeam.Radiant);

        Assert.True(result.Success);
        Assert.Contains("Winner: Radiant", result.Message);
        Assert.Equal(MatchStatus.Completed, result.Match!.Status);
        Assert.Equal(MatchTeam.Radiant, result.Match.WinningTeam);

        // Even 1000v1000 match → winner +16, loser -16 (K-factor 32).
        await using var ctx = await factory.NewContextAsync();
        var radiant = await ctx.Players.FindAsync(1UL);
        var dire = await ctx.Players.FindAsync(2UL);
        Assert.Equal(1016, radiant!.Rating);
        Assert.Equal(1, radiant.Wins);
        Assert.Equal(0, radiant.Losses);
        Assert.Equal(984, dire!.Rating);
        Assert.Equal(0, dire.Wins);
        Assert.Equal(1, dire.Losses);
    }

    [Fact]
    public async Task ReportMatch_NoActiveMatch_Fails()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new MatchRepository(factory.Object);

        var result = await repo.ReportMatchAsync(reporterUserId: 1, winningTeam: MatchTeam.Radiant);

        Assert.False(result.Success);
        Assert.Contains("active match", result.Message);
    }

    [Fact]
    public async Task CompleteMatchByLobby_WithWinner_CompletesAndAppliesRating()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new MatchRepository(factory.Object);
        await SeedPlayersAsync(factory, Player(1, rating: 1000), Player(2, rating: 1000));
        await repo.CreateMatchAsync("lobby-1", [(1UL, MatchTeam.Radiant), (2UL, MatchTeam.Dire)]);

        var result = await repo.CompleteMatchByLobbyAsync("lobby-1", MatchTeam.Dire);

        Assert.True(result.Success);
        Assert.Equal(MatchStatus.Completed, result.Match!.Status);

        await using var ctx = await factory.NewContextAsync();
        Assert.Equal(984, (await ctx.Players.FindAsync(1UL))!.Rating); // Radiant lost
        Assert.Equal(1016, (await ctx.Players.FindAsync(2UL))!.Rating); // Dire won
    }

    [Fact]
    public async Task CompleteMatchByLobby_NullWinner_CancelsWithoutRatingChange()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new MatchRepository(factory.Object);
        await SeedPlayersAsync(factory, Player(1, rating: 1000), Player(2, rating: 1000));
        await repo.CreateMatchAsync("lobby-1", [(1UL, MatchTeam.Radiant), (2UL, MatchTeam.Dire)]);

        var result = await repo.CompleteMatchByLobbyAsync("lobby-1", winningTeam: null);

        Assert.True(result.Success);
        Assert.Equal(MatchStatus.Cancelled, result.Match!.Status);

        await using var ctx = await factory.NewContextAsync();
        Assert.Equal(1000, (await ctx.Players.FindAsync(1UL))!.Rating); // untouched
        Assert.Equal(1000, (await ctx.Players.FindAsync(2UL))!.Rating);
    }

    [Fact]
    public async Task CompleteMatchByLobby_AlreadySettled_IsIdempotent()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new MatchRepository(factory.Object);
        await SeedPlayersAsync(factory, Player(1, rating: 1000), Player(2, rating: 1000));
        await repo.CreateMatchAsync("lobby-1", [(1UL, MatchTeam.Radiant), (2UL, MatchTeam.Dire)]);
        await repo.CompleteMatchByLobbyAsync("lobby-1", MatchTeam.Radiant);

        // Second settle of the same lobby must not re-apply rating.
        var second = await repo.CompleteMatchByLobbyAsync("lobby-1", MatchTeam.Radiant);

        Assert.True(second.Success);
        Assert.Contains("already settled", second.Message);

        await using var ctx = await factory.NewContextAsync();
        Assert.Equal(1016, (await ctx.Players.FindAsync(1UL))!.Rating); // still just one application
    }

    [Fact]
    public async Task CompleteMatchByLobby_UnknownLobby_Fails()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new MatchRepository(factory.Object);

        var result = await repo.CompleteMatchByLobbyAsync("ghost-lobby", MatchTeam.Radiant);

        Assert.False(result.Success);
        Assert.Contains("No match found", result.Message);
    }

    [Fact]
    public async Task GetStaleInProgressMatches_ReturnsOnlyOldInProgressMatches()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new MatchRepository(factory.Object);
        await using (var ctx = await factory.NewContextAsync())
        {
            ctx.Matches.AddRange(
                new MatchEntity
                {
                    Id = Guid.NewGuid(),
                    Status = MatchStatus.InProgress,
                    CreatedAt = DateTime.UtcNow.AddHours(-3),
                }, // stale
                new MatchEntity
                {
                    Id = Guid.NewGuid(),
                    Status = MatchStatus.InProgress,
                    CreatedAt = DateTime.UtcNow,
                }, // fresh
                new MatchEntity
                {
                    Id = Guid.NewGuid(),
                    Status = MatchStatus.Completed,
                    CreatedAt = DateTime.UtcNow.AddHours(-5),
                }
            ); // settled
            await ctx.SaveChangesAsync();
        }

        var stale = await repo.GetStaleInProgressMatchesAsync(DateTime.UtcNow.AddHours(-2));

        var only = Assert.Single(stale);
        Assert.Equal(MatchStatus.InProgress, only.Status);
        Assert.True(only.CreatedAt < DateTime.UtcNow.AddHours(-2));
    }

    [Fact]
    public async Task CancelMatch_InProgress_CancelsThenIsNoOpOnRepeat()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new MatchRepository(factory.Object);
        var match = await repo.CreateMatchAsync("lobby-1", [(1UL, MatchTeam.Radiant)]);

        Assert.True(await repo.CancelMatchAsync(match.Id));
        Assert.Equal(MatchStatus.Cancelled, (await repo.GetByIdAsync(match.Id))!.Status);

        Assert.False(await repo.CancelMatchAsync(match.Id)); // already settled
        Assert.False(await repo.CancelMatchAsync(Guid.NewGuid())); // unknown match
    }

    [Fact]
    public async Task GetRecentMatchesForPlayer_ReturnsCompletedMatchesOnly()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new MatchRepository(factory.Object);
        await SeedPlayersAsync(factory, Player(1), Player(2));

        // One completed, one still in progress.
        await repo.CreateMatchAsync(
            "lobby-done",
            [(1UL, MatchTeam.Radiant), (2UL, MatchTeam.Dire)]
        );
        await repo.CompleteMatchByLobbyAsync("lobby-done", MatchTeam.Radiant);
        await repo.CreateMatchAsync(
            "lobby-live",
            [(1UL, MatchTeam.Radiant), (2UL, MatchTeam.Dire)]
        );

        var recent = await repo.GetRecentMatchesForPlayerAsync(1, top: 5);

        Assert.Equal(1, recent.Count);
        Assert.Equal(1UL, recent.UserId);
        Assert.All(recent.Matches, m => Assert.Equal(MatchStatus.Completed, m.Status));
    }
}
