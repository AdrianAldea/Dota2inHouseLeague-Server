using IHLDota2Bot;
using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot.Tests;

public class EloCalculatorTests
{
    // ── Even matches: with K=32 the winner gains exactly half the K-factor ──

    [Fact]
    public void ComputeDeltas_EqualRatings_RadiantWins_RadiantGainsHalfKFactor()
    {
        var (radiant, dire) = EloCalculator.ComputeDeltas([1000], [1000], MatchTeam.Radiant);

        Assert.Equal(16, radiant); // round(32 * (1 - 0.5))
        Assert.Equal(-16, dire);
    }

    [Fact]
    public void ComputeDeltas_EqualRatings_DireWins_MirrorsRadiantLoss()
    {
        var (radiant, dire) = EloCalculator.ComputeDeltas([1000], [1000], MatchTeam.Dire);

        Assert.Equal(-16, radiant);
        Assert.Equal(16, dire);
    }

    // ── Upset weighting: the favourite gains little for an expected win and
    //    loses a lot for an upset; the underdog is the inverse. ──

    [Fact]
    public void ComputeDeltas_FavouriteWins_GainsFewerPointsThanEvenMatch()
    {
        // Radiant favoured by 200 points and wins as expected.
        var (radiant, dire) = EloCalculator.ComputeDeltas([1200], [1000], MatchTeam.Radiant);

        Assert.Equal(8, radiant); // round(32 * (1 - 0.7597)) — less than the even-match 16
        Assert.Equal(-8, dire);
    }

    [Fact]
    public void ComputeDeltas_FavouriteLoses_DropsMorePointsThanEvenMatch()
    {
        // Radiant favoured by 200 points but loses the upset.
        var (radiant, dire) = EloCalculator.ComputeDeltas([1200], [1000], MatchTeam.Dire);

        Assert.Equal(-24, radiant); // round(32 * (0 - 0.7597)) — bigger swing than even-match 16
        Assert.Equal(24, dire);
    }

    [Fact]
    public void ComputeDeltas_UnderdogWin_IsLargerThanFavouriteWin()
    {
        var (favouriteWin, _) = EloCalculator.ComputeDeltas([1200], [1000], MatchTeam.Radiant);
        var (_, underdogWin) = EloCalculator.ComputeDeltas([1200], [1000], MatchTeam.Dire);

        Assert.True(underdogWin > favouriteWin);
    }

    // ── Team averages drive the expectation, not individual ratings ──

    [Fact]
    public void ComputeDeltas_UsesTeamAverages_NotRawTotals()
    {
        // Both averages are 1000 → behaves exactly like an even 1v1.
        var (radiant, dire) = EloCalculator.ComputeDeltas(
            [800, 1200],
            [900, 1100],
            MatchTeam.Radiant
        );

        Assert.Equal(16, radiant);
        Assert.Equal(-16, dire);
    }

    // ── Guard clauses: a side with no rated players never moves anyone ──

    [Theory]
    [InlineData(MatchTeam.Radiant)]
    [InlineData(MatchTeam.Dire)]
    public void ComputeDeltas_EmptyRadiant_ReturnsZero(MatchTeam winner)
    {
        var result = EloCalculator.ComputeDeltas([], [1000], winner);
        Assert.Equal((0, 0), result);
    }

    [Theory]
    [InlineData(MatchTeam.Radiant)]
    [InlineData(MatchTeam.Dire)]
    public void ComputeDeltas_EmptyDire_ReturnsZero(MatchTeam winner)
    {
        var result = EloCalculator.ComputeDeltas([1000], [], winner);
        Assert.Equal((0, 0), result);
    }

    [Fact]
    public void ComputeDeltas_BothEmpty_ReturnsZero()
    {
        var result = EloCalculator.ComputeDeltas([], [], MatchTeam.Radiant);
        Assert.Equal((0, 0), result);
    }

    // ── Invariant: the formula is strictly zero-sum for any valid roster ──

    [Theory]
    [InlineData(1000, 1000, MatchTeam.Radiant)]
    [InlineData(1500, 900, MatchTeam.Dire)]
    [InlineData(800, 2000, MatchTeam.Radiant)]
    [InlineData(1234, 1233, MatchTeam.Dire)]
    public void ComputeDeltas_IsZeroSum(int radiantRating, int direRating, MatchTeam winner)
    {
        var (radiant, dire) = EloCalculator.ComputeDeltas([radiantRating], [direRating], winner);
        Assert.Equal(0, radiant + dire);
    }

    [Theory]
    [InlineData(1000, 1000, MatchTeam.Radiant)]
    [InlineData(2400, 100, MatchTeam.Dire)] // extreme mismatch
    [InlineData(100, 2400, MatchTeam.Radiant)]
    public void ComputeDeltas_MagnitudeNeverExceedsKFactor(
        int radiantRating,
        int direRating,
        MatchTeam winner
    )
    {
        var (radiant, _) = EloCalculator.ComputeDeltas([radiantRating], [direRating], winner);
        Assert.True(Math.Abs(radiant) <= LeagueConstants.EloKFactor);
    }
}
