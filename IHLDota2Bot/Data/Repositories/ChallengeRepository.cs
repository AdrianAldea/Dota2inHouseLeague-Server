using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IHLDota2Bot.Data.Repositories;

public class ChallengeRepository : IChallengeRepository
{
    private readonly IDbContextFactory<AppDbContext> contextFactory;

    public ChallengeRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        this.contextFactory = contextFactory;
    }

    public async Task<ChallengeEntity> CreateAsync(
        ulong challengerUserId,
        ulong challengedUserId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.UtcNow;

        // Expire stale outgoing pending challenges from this challenger.
        var existing = await context.Challenges
            .Where(c => c.ChallengerUserId == challengerUserId && c.Status == ChallengeStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var c in existing)
        {
            c.Status = ChallengeStatus.Cancelled;
            c.RespondedAt = now;
        }

        var challenge = new ChallengeEntity
        {
            Id = Guid.NewGuid(),
            ChallengerUserId = challengerUserId,
            ChallengedUserId = challengedUserId,
            CreatedAt = now,
            ExpiresAt = now.Add(ttl),
            Status = ChallengeStatus.Pending
        };

        context.Challenges.Add(challenge);
        await context.SaveChangesAsync(cancellationToken);
        return challenge;
    }

    public async Task<ChallengeEntity?> GetActiveIncomingAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await ExpireStaleAsync(context, cancellationToken);

        return await context.Challenges
            .AsNoTracking()
            .Where(c => c.ChallengedUserId == userId && c.Status == ChallengeStatus.Pending)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ChallengeEntity?> GetActiveOutgoingAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await ExpireStaleAsync(context, cancellationToken);

        return await context.Challenges
            .AsNoTracking()
            .Where(c => c.ChallengerUserId == userId && c.Status == ChallengeStatus.Pending)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ChallengeEntity?> AcceptLatestAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await ExpireStaleAsync(context, cancellationToken);

        var challenge = await context.Challenges
            .Where(c => c.ChallengedUserId == userId && c.Status == ChallengeStatus.Pending)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (challenge == null) return null;

        challenge.Status = ChallengeStatus.Accepted;
        challenge.RespondedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return challenge;
    }

    public async Task<ChallengeEntity?> DeclineLatestAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await ExpireStaleAsync(context, cancellationToken);

        var challenge = await context.Challenges
            .Where(c => c.ChallengedUserId == userId && c.Status == ChallengeStatus.Pending)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (challenge == null) return null;

        challenge.Status = ChallengeStatus.Declined;
        challenge.RespondedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return challenge;
    }

    public async Task SetResultingMatchAsync(Guid challengeId, Guid matchId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var challenge = await context.Challenges.FirstOrDefaultAsync(c => c.Id == challengeId, cancellationToken);
        if (challenge == null) return;

        challenge.ResultingMatchId = matchId;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task ExpireStaleAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var stale = await context.Challenges
            .Where(c => c.Status == ChallengeStatus.Pending && c.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0) return;

        foreach (var c in stale)
        {
            c.Status = ChallengeStatus.Expired;
            c.RespondedAt = now;
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
