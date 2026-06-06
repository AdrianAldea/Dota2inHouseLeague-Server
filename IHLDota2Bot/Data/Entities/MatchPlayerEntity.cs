using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot.Data.Entities;

public class MatchPlayerEntity
{
    public Guid MatchId { get; set; }
    public ulong UserId { get; set; }
    public MatchTeam Team { get; set; }
    public bool Won { get; set; }

    public MatchEntity Match { get; set; } = null!;
    public PlayerEntity Player { get; set; } = null!;
}
