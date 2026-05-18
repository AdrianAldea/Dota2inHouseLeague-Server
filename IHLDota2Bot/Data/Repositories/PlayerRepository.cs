using IHLDota2Bot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IHLDota2Bot.Data.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly IDbContextFactory<AppDbContext> contextFactory;

    public PlayerRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        this.contextFactory = contextFactory;
    }

    public async Task<PlayerEntity?> GetByUserIdAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task<PlayerEntity?> GetBySteamIdAsync(string steamId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.SteamId == steamId, cancellationToken);
    }

    public async Task<List<PlayerEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Players
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<PlayerEntity> UpsertAsync(PlayerEntity player, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(player);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.Players.FirstOrDefaultAsync(p => p.UserId == player.UserId, cancellationToken);

        if (existing == null)
        {
            player.RegisteredAt = player.RegisteredAt == default ? DateTime.UtcNow : player.RegisteredAt;
            player.LastSeenAt = DateTime.UtcNow;
            context.Players.Add(player);
        }
        else
        {
            existing.Username = player.Username;
            existing.SteamId = player.SteamId;
            existing.LastSeenAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        return existing ?? player;
    }

    public async Task<bool> ExistsAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Players.AnyAsync(p => p.UserId == userId, cancellationToken);
    }
}
