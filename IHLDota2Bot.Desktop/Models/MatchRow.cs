namespace IHLDota2Bot.Desktop.Models;

/// <summary>
/// Display-oriented match-history row. Phase 4 maps these from <c>MatchSummary</c>
/// (resolving the player's team + win/loss); for now it carries pre-formatted text.
/// </summary>
public sealed class MatchRow
{
    public required bool IsWin { get; init; }
    public required string Mode { get; init; }
    public required string When { get; init; }
    public required string RatingChange { get; init; }
    public string Result => IsWin ? "WIN" : "LOSS";
}
