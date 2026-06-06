using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot.Data.Entities;

public class ChallengeEntity
{
    public Guid Id { get; set; }
    public ulong ChallengerUserId { get; set; }
    public ulong ChallengedUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public ChallengeStatus Status { get; set; }
    public DateTime? RespondedAt { get; set; }
    public Guid? ResultingMatchId { get; set; }
}
