using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot.Tests;

public class TeamBalancerTests
{
    private static (List<ulong> Radiant, List<ulong> Dire) Split(
        List<(ulong UserId, MatchTeam Team)> assignments
    ) =>
        (
            assignments.Where(a => a.Team == MatchTeam.Radiant).Select(a => a.UserId).ToList(),
            assignments.Where(a => a.Team == MatchTeam.Dire).Select(a => a.UserId).ToList()
        );

    [Fact]
    public void Balance_EmptyInput_ReturnsEmpty() => Assert.Empty(TeamBalancer.Balance([]));

    [Fact]
    public void Balance_TwoPlayers_PutsOneOnEachTeam()
    {
        var (radiant, dire) = Split(TeamBalancer.Balance([(1UL, 1200), (2UL, 1000)]));

        Assert.Single(radiant);
        Assert.Single(dire);
    }

    [Fact]
    public void Balance_KnownRatings_PairsStrongestWithWeakest()
    {
        // Greedy on 1400/1200/1100/900 must produce 1400+900 vs 1200+1100 —
        // a perfect 2300 vs 2300 split. A naive top-half/bottom-half cut
        // (1400+1200 vs 1100+900) would be 600 apart.
        var assignments = TeamBalancer.Balance([(1UL, 1400), (2UL, 1200), (3UL, 1100), (4UL, 900)]);

        var (radiant, dire) = Split(assignments);
        Assert.Equal(2, radiant.Count);
        Assert.Equal(2, dire.Count);
        Assert.True(
            radiant.ToHashSet().SetEquals([1UL, 4UL]) || dire.ToHashSet().SetEquals([1UL, 4UL]),
            "strongest and weakest should be teammates"
        );
    }

    [Fact]
    public void Balance_EqualRatings_AlternatesInputOrder()
    {
        // All ties: the stable sort keeps input order and the greedy pass
        // alternates, so shuffling the input (as the caller does) varies teams.
        var players = Enumerable.Range(1, 10).Select(i => ((ulong)i, 1000)).ToList();

        var assignments = TeamBalancer.Balance(players);

        var (radiant, dire) = Split(assignments);
        Assert.Equal(5, radiant.Count);
        Assert.Equal(5, dire.Count);
        Assert.Equal([1UL, 3UL, 5UL, 7UL, 9UL], radiant);
        Assert.Equal([2UL, 4UL, 6UL, 8UL, 10UL], dire);
    }

    [Fact]
    public void Balance_OddCount_GivesDireTheExtraSeat()
    {
        // Matches the previous random split's convention: Radiant = count/2.
        var (radiant, dire) = Split(TeamBalancer.Balance([(1UL, 1200), (2UL, 1000), (3UL, 800)]));

        Assert.Single(radiant);
        Assert.Equal(2, dire.Count);
    }

    [Fact]
    public void Balance_OneRunawayRating_StillFillsBothTeamsEvenly()
    {
        // Greedy wants to keep stacking Dire (its sum stays lower), but the
        // size cap must force the final player onto Radiant: 2/2, not 1/3.
        var (radiant, dire) = Split(
            TeamBalancer.Balance([(1UL, 4000), (2UL, 1000), (3UL, 1000), (4UL, 1000)])
        );

        Assert.Equal(2, radiant.Count);
        Assert.Equal(2, dire.Count);
    }

    [Theory]
    [InlineData(new[] { 1400, 1200, 1100, 900 })]
    [InlineData(new[] { 2000, 1000, 1000, 1000 })]
    [InlineData(new[] { 1500, 1450, 1300, 1250, 1100, 1050, 1000, 950, 900, 850 })]
    public void Balance_NeverWorseThanNaiveTopBottomSplit(int[] ratings)
    {
        var players = ratings.Select((r, i) => ((ulong)(i + 1), r)).ToList();

        var assignments = TeamBalancer.Balance(players);

        var byUser = players.ToDictionary(p => p.Item1, p => p.Item2);
        var (radiant, dire) = Split(assignments);
        var greedyGap = Math.Abs(radiant.Sum(u => byUser[u]) - dire.Sum(u => byUser[u]));

        // Naive split: top half of the descending sort vs the bottom half.
        var sorted = ratings.OrderByDescending(r => r).ToList();
        var naiveGap = Math.Abs(
            sorted.Take(ratings.Length / 2).Sum() - sorted.Skip(ratings.Length / 2).Sum()
        );

        Assert.True(greedyGap <= naiveGap);
    }
}
