namespace IHLDota2Bot.Desktop.Models;

/// <summary>
/// One player row in the match-found team rosters. <see cref="IsMe"/> highlights
/// the signed-in player so they can spot their side at a glance.
/// </summary>
public sealed class TeamSlot
{
    public required string Name { get; init; }
    public bool IsMe { get; init; }
    public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name[..1].ToUpperInvariant();
}
