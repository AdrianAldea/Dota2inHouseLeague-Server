namespace IHLDota2Bot.Desktop.Models;

/// <summary>
/// A visual rank tier derived from a player's rating — Dota-medal-style flavour for the
/// premium look. Thresholds are presentation-only (the backend rating is the source of truth).
/// </summary>
public readonly record struct RankTier(string Name, string ColorHex)
{
    public static RankTier For(int rating) => rating switch
    {
        < 900  => new RankTier("Herald",   "#9BA3B4"),
        < 1050 => new RankTier("Guardian", "#6FCF97"),
        < 1200 => new RankTier("Crusader", "#56CCF2"),
        < 1350 => new RankTier("Archon",   "#9B8CFF"),
        < 1500 => new RankTier("Legend",   "#BB6BD9"),
        < 1650 => new RankTier("Ancient",  "#F2994A"),
        < 1800 => new RankTier("Divine",   "#FF6A6A"),
        _      => new RankTier("Immortal", "#FFB23D"),
    };
}
