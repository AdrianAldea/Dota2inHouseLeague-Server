using IHLDota2Bot.Data.Entities;
using IHLDota2Bot.Data.Repositories;
using IHLDota2Bot.Tests.TestSupport;

namespace IHLDota2Bot.Tests;

/// <summary>
/// Exercises <see cref="PlayerRepository"/> against a real EF Core in-memory store
/// (the <see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{T}"/> is a Moq
/// stub handing back real contexts). This validates the actual LINQ ordering,
/// ranking and projection logic — the cases that matter in production — rather
/// than re-asserting a hand-stubbed query.
/// </summary>
public class PlayerRepositoryTests
{
    private static PlayerEntity Player(
        ulong id,
        string name,
        int rating = 1000,
        int wins = 0,
        int losses = 0
    ) =>
        new()
        {
            UserId = id,
            Username = name,
            SteamId = $"steam-{id}",
            Rating = rating,
            Wins = wins,
            Losses = losses,
            RegisteredAt = DateTime.UtcNow,
        };

    [Fact]
    public async Task Upsert_NewPlayer_InsertsWithTimestamps()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new PlayerRepository(factory.Object);

        var saved = await repo.UpsertAsync(Player(1, "alice"));

        Assert.Equal(1UL, saved.UserId);
        Assert.NotEqual(default, saved.RegisteredAt);
        Assert.NotNull(saved.LastSeenAt);
        Assert.True(await repo.ExistsAsync(1));
    }

    [Fact]
    public async Task Upsert_ExistingPlayer_UpdatesProfileButKeepsRating()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new PlayerRepository(factory.Object);
        await repo.UpsertAsync(Player(1, "alice", rating: 1234));

        await repo.UpsertAsync(Player(1, "alice_renamed", rating: 1000)); // rating on the incoming entity is ignored

        var fetched = await repo.GetByUserIdAsync(1);
        Assert.Equal("alice_renamed", fetched!.Username);
        Assert.Equal(1234, fetched.Rating); // unchanged by upsert
        Assert.Single(await repo.GetAllAsync());
    }

    [Fact]
    public async Task GetBySteamId_ReturnsMatchingPlayer()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new PlayerRepository(factory.Object);
        await repo.UpsertAsync(Player(1, "alice"));

        var bySteam = await repo.GetBySteamIdAsync("steam-1");

        Assert.NotNull(bySteam);
        Assert.Equal(1UL, bySteam!.UserId);
        Assert.Null(await repo.GetBySteamIdAsync("nope"));
    }

    [Fact]
    public async Task UpdateSteamId_KnownPlayer_PersistsNewId_UnknownReturnsNull()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new PlayerRepository(factory.Object);
        await repo.UpsertAsync(Player(1, "alice"));

        var updated = await repo.UpdateSteamIdAsync(1, "steam-new");
        Assert.Equal("steam-new", updated!.SteamId);

        Assert.Null(await repo.UpdateSteamIdAsync(999, "x"));
    }

    [Fact]
    public async Task Delete_RemovesPlayer_ThenReportsMissing()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new PlayerRepository(factory.Object);
        await repo.UpsertAsync(Player(1, "alice"));

        Assert.True(await repo.DeleteAsync(1));
        Assert.False(await repo.ExistsAsync(1));
        Assert.False(await repo.DeleteAsync(1)); // already gone
    }

    [Fact]
    public async Task SetBan_BansThenUnbans()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new PlayerRepository(factory.Object);
        await repo.UpsertAsync(Player(1, "alice"));

        var banned = await repo.SetBanAsync(1, banned: true, reason: "smurfing");
        Assert.True(banned!.IsBanned);
        Assert.Equal("smurfing", banned.BanReason);
        Assert.NotNull(banned.BannedAt);

        var unbanned = await repo.SetBanAsync(1, banned: false, reason: null);
        Assert.False(unbanned!.IsBanned);
        Assert.Null(unbanned.BanReason);
        Assert.Null(unbanned.BannedAt);

        Assert.Null(await repo.SetBanAsync(999, true, "x")); // unknown user
    }

    [Fact]
    public async Task GetStats_ReturnsStats_WithComputedWinRate()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new PlayerRepository(factory.Object);
        await repo.UpsertAsync(Player(1, "alice", rating: 1100, wins: 3, losses: 1));

        var stats = await repo.GetStatsAsync(1);

        Assert.NotNull(stats);
        Assert.Equal(1100, stats!.Rating);
        Assert.Equal(4, stats.MatchesPlayed);
        Assert.Equal(0.75, stats.WinRate, 3);
        Assert.Null(await repo.GetStatsAsync(999));
    }

    [Fact]
    public async Task GetLeaderboard_OrdersByRatingThenWinsThenLossesThenName()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new PlayerRepository(factory.Object);
        await using (var ctx = await factory.NewContextAsync())
        {
            ctx.Players.AddRange(
                Player(1, "Alice", rating: 1200, wins: 5, losses: 1),
                Player(2, "Bob", rating: 1200, wins: 5, losses: 3), // same rating+wins, more losses → after Alice
                Player(3, "Cara", rating: 1100, wins: 10, losses: 0), // lower rating → last despite most wins
                Player(4, "Dan", rating: 1200, wins: 7, losses: 0)
            ); // most wins in the 1200 tier → first
            await ctx.SaveChangesAsync();
        }

        var board = await repo.GetLeaderboardAsync(10);

        Assert.Equal(["Dan", "Alice", "Bob", "Cara"], board.Entries.Select(e => e.Username));
        Assert.Equal([1, 2, 3, 4], board.Entries.Select(e => e.Rank));
    }

    [Fact]
    public async Task GetLeaderboard_RespectsTopN()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new PlayerRepository(factory.Object);
        await using (var ctx = await factory.NewContextAsync())
        {
            ctx.Players.AddRange(
                Player(1, "Alice", rating: 1300),
                Player(2, "Bob", rating: 1200),
                Player(3, "Cara", rating: 1100)
            );
            await ctx.SaveChangesAsync();
        }

        var board = await repo.GetLeaderboardAsync(2);

        Assert.Equal(2, board.Entries.Count);
        Assert.Equal("Alice", board.Entries[0].Username);
        Assert.Equal("Bob", board.Entries[1].Username);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetLeaderboard_NonPositiveTopN_DefaultsToTen(int requested)
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new PlayerRepository(factory.Object);

        var board = await repo.GetLeaderboardAsync(requested);

        Assert.Equal(10, board.TopN);
    }

    [Fact]
    public async Task GetRank_ComputesRankAndPercentileWithTieBreaks()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new PlayerRepository(factory.Object);
        await using (var ctx = await factory.NewContextAsync())
        {
            ctx.Players.AddRange(
                Player(1, "Alice", rating: 1200, wins: 5, losses: 1),
                Player(2, "Bob", rating: 1200, wins: 5, losses: 3), // our subject
                Player(3, "Cara", rating: 1100, wins: 10, losses: 0),
                Player(4, "Dan", rating: 1200, wins: 7, losses: 0)
            );
            await ctx.SaveChangesAsync();
        }

        var rank = await repo.GetRankAsync(2); // Bob — two players rank above him (Dan, Alice)

        Assert.NotNull(rank);
        Assert.Equal(3, rank!.Rank);
        Assert.Equal(4, rank.TotalPlayers);
        Assert.Equal(75d, rank.Percentile, 3);
        Assert.Equal("Bob", rank.Stats.Username);
    }

    [Fact]
    public async Task GetRank_UnknownPlayer_ReturnsNull()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new PlayerRepository(factory.Object);

        Assert.Null(await repo.GetRankAsync(123));
    }

    [Fact]
    public async Task GetRank_SinglePlayer_PercentileIsZero()
    {
        var factory = InMemoryDb.CreateFactory();
        var repo = new PlayerRepository(factory.Object);
        await repo.UpsertAsync(Player(1, "alice"));

        var rank = await repo.GetRankAsync(1);

        Assert.Equal(1, rank!.Rank);
        Assert.Equal(1, rank.TotalPlayers);
        Assert.Equal(0d, rank.Percentile);
    }
}
