using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot;

/// <summary>
/// Pure ELO rating math for a completed match. Deliberately free of EF Core and
/// entity types so the formula lives in one place and can be unit-tested in
/// isolation from the database.
/// </summary>
public static class EloCalculator
{
    /// <summary>
    /// Computes the rating change applied to each team from their average ratings.
    /// Returns <c>(radiantDelta, direDelta)</c> where <c>direDelta == -radiantDelta</c>
    /// (zero-sum). Returns <c>(0, 0)</c> when either side has no rated players, so a
    /// lopsided or empty roster never moves anyone's rating.
    /// </summary>
    public static (int RadiantDelta, int DireDelta) ComputeDeltas(
        IReadOnlyCollection<int> radiantRatings,
        IReadOnlyCollection<int> direRatings,
        MatchTeam winningTeam
    )
    {
        if (radiantRatings.Count == 0 || direRatings.Count == 0)
        {
            return (0, 0);
        }

        var avgRadiant = radiantRatings.Average();
        var avgDire = direRatings.Average();
        var expectedRadiant = 1.0 / (1.0 + Math.Pow(10, (avgDire - avgRadiant) / 400.0));
        var actualRadiant = winningTeam == MatchTeam.Radiant ? 1.0 : 0.0;
        var radiantDelta = (int)
            Math.Round(LeagueConstants.EloKFactor * (actualRadiant - expectedRadiant));
        return (radiantDelta, -radiantDelta);
    }
}
