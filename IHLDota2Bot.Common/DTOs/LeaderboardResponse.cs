namespace IHLDota2Bot.Common.DTOs;

public record LeaderboardResponse
{
    public required int TopN { get; init; }
    public required IReadOnlyList<LeaderboardEntry> Entries { get; init; }
}

public record LeaderboardEntry
{
    public required int Rank { get; init; }
    public required ulong UserId { get; init; }
    public required string Username { get; init; }
    public required int Rating { get; init; }
    public required int Wins { get; init; }
    public required int Losses { get; init; }

    public int MatchesPlayed => Wins + Losses;
    public double WinRate => MatchesPlayed == 0 ? 0d : (double)Wins / MatchesPlayed;
}
