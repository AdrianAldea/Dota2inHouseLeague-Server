using IHLDota2Bot.Data.Entities;

namespace IHLDota2Bot.Data.Repositories;

public interface IChallengeRepository
{
    /// <summary>
    /// Creates a new pending challenge. Cancels any prior pending outgoing
    /// challenge from the same challenger so each user has at most one outgoing.
    /// </summary>
    Task<ChallengeEntity> CreateAsync(
        ulong challengerUserId,
        ulong challengedUserId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Returns the most recent non-expired pending challenge directed at userId, if any.
    /// Also lazily marks expired challenges.
    /// </summary>
    Task<ChallengeEntity?> GetActiveIncomingAsync(
        ulong userId,
        CancellationToken cancellationToken = default
    );

    Task<ChallengeEntity?> GetActiveOutgoingAsync(
        ulong userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Marks the latest pending incoming challenge for userId as accepted.
    /// Returns the accepted challenge or null if none was pending.
    /// </summary>
    Task<ChallengeEntity?> AcceptLatestAsync(
        ulong userId,
        CancellationToken cancellationToken = default
    );

    Task<ChallengeEntity?> DeclineLatestAsync(
        ulong userId,
        CancellationToken cancellationToken = default
    );

    Task SetResultingMatchAsync(
        Guid challengeId,
        Guid matchId,
        CancellationToken cancellationToken = default
    );
}
