using System.Collections.Generic;
using System.Linq;
using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot.Desktop.Models;

/// <summary>One player line inside a match-detail team list.</summary>
public sealed class MatchPlayerLine
{
    public required string Name { get; init; }
    public required bool Won { get; init; }
    public bool IsMe { get; init; }
}

/// <summary>
/// A match-history entry that carries both the compact row fields and the full team
/// breakdown for the detail overlay. Built from a <see cref="MatchSummary"/>.
/// </summary>
public sealed class MatchListItem
{
    public required bool IsWin { get; init; }
    public string Result => IsWin ? "WIN" : "LOSS";
    public required string Mode { get; init; }
    public required string When { get; init; }
    public required bool RadiantWon { get; init; }
    public bool DireWon => !RadiantWon;
    public string RadiantResult => RadiantWon ? "Victory" : "Defeat";
    public string DireResult => RadiantWon ? "Defeat" : "Victory";
    public required IReadOnlyList<MatchPlayerLine> Radiant { get; init; }
    public required IReadOnlyList<MatchPlayerLine> Dire { get; init; }

    public static MatchListItem FromSummary(MatchSummary match, ulong myId, string when)
    {
        var mine = match.Players.FirstOrDefault(p => p.UserId == myId);
        return new MatchListItem
        {
            IsWin = mine?.Won ?? false,
            Mode = "5v5 Match",
            When = when,
            RadiantWon = match.WinningTeam == MatchTeam.Radiant,
            Radiant = Lines(match, MatchTeam.Radiant, myId),
            Dire = Lines(match, MatchTeam.Dire, myId),
        };
    }

    private static List<MatchPlayerLine> Lines(MatchSummary match, MatchTeam team, ulong myId) =>
        match
            .Players.Where(p => p.Team == team)
            .Select(p => new MatchPlayerLine
            {
                Name = p.Username,
                Won = p.Won,
                IsMe = p.UserId == myId,
            })
            .ToList();
}
