using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot;

/// <summary>
/// Splits a full lobby into two rating-balanced teams. Pure and deterministic:
/// players are sorted by rating (descending, stable — input order breaks ties),
/// then greedily assigned to the team with the lower rating total that still has
/// room. Callers that want rating ties broken randomly (common in a new league
/// where everyone sits at the default rating) shuffle the input first.
/// </summary>
public static class TeamBalancer
{
    public static List<(ulong UserId, MatchTeam Team)> Balance(
        IReadOnlyList<(ulong UserId, int Rating)> players
    )
    {
        // Same size convention as the previous random split: Radiant gets
        // count/2, Dire the remainder (the extra seat on an odd count).
        var radiantCapacity = players.Count / 2;
        var direCapacity = players.Count - radiantCapacity;

        var assignments = new List<(ulong UserId, MatchTeam Team)>(players.Count);
        int radiantSum = 0,
            direSum = 0,
            radiantCount = 0,
            direCount = 0;

        foreach (var (userId, rating) in players.OrderByDescending(p => p.Rating))
        {
            var toRadiant =
                radiantCount < radiantCapacity
                && (direCount >= direCapacity || radiantSum <= direSum);

            if (toRadiant)
            {
                assignments.Add((userId, MatchTeam.Radiant));
                radiantSum += rating;
                radiantCount++;
            }
            else
            {
                assignments.Add((userId, MatchTeam.Dire));
                direSum += rating;
                direCount++;
            }
        }

        return assignments;
    }
}
