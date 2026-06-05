using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot.Data.Entities;

public class MatchEntity
{
    public Guid Id { get; set; }
    public string? LobbyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public MatchStatus Status { get; set; }
    public MatchTeam? WinningTeam { get; set; }

    public List<MatchPlayerEntity> Players { get; set; } = [];
}
