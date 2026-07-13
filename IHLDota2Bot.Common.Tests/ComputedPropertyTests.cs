using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot.Common.Tests;

/// <summary>
/// The DTOs are mostly data carriers, but a few expose derived values
/// (<c>MatchesPlayed</c>, <c>WinRate</c>) that the Discord formatter and the web
/// UI render directly. These tests pin that arithmetic, including the divide-by-zero
/// guard for a player who has never finished a match.
/// </summary>
public class ComputedPropertyTests
{
    [Theory]
    [InlineData(3, 1, 4, 0.75)]
    [InlineData(0, 0, 0, 0.0)] // never played → no division by zero
    [InlineData(5, 5, 10, 0.5)]
    [InlineData(10, 0, 10, 1.0)]
    [InlineData(0, 7, 7, 0.0)]
    public void PlayerStatsResponse_ComputesMatchesPlayedAndWinRate(
        int wins,
        int losses,
        int expectedPlayed,
        double expectedWinRate
    )
    {
        var stats = new PlayerStatsResponse
        {
            UserId = 1,
            Username = "alice",
            SteamId = "steam-1",
            Rating = 1000,
            Wins = wins,
            Losses = losses,
        };

        Assert.Equal(expectedPlayed, stats.MatchesPlayed);
        Assert.Equal(expectedWinRate, stats.WinRate, 5);
    }

    [Theory]
    [InlineData(2, 2, 4, 0.5)]
    [InlineData(0, 0, 0, 0.0)]
    [InlineData(9, 1, 10, 0.9)]
    public void LeaderboardEntry_ComputesMatchesPlayedAndWinRate(
        int wins,
        int losses,
        int expectedPlayed,
        double expectedWinRate
    )
    {
        var entry = new LeaderboardEntry
        {
            Rank = 1,
            UserId = 1,
            Username = "alice",
            Rating = 1000,
            Wins = wins,
            Losses = losses,
        };

        Assert.Equal(expectedPlayed, entry.MatchesPlayed);
        Assert.Equal(expectedWinRate, entry.WinRate, 5);
    }
}
