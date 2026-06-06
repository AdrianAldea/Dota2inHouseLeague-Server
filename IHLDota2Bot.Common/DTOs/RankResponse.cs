namespace IHLDota2Bot.Common.DTOs;

public record RankResponse
{
    public required int Rank { get; init; }
    public required int TotalPlayers { get; init; }
    /// <summary>"Top X%" — lower is better. 0 if no players or only one player.</summary>
    public required double Percentile { get; init; }
    public required PlayerStatsResponse Stats { get; init; }
}
