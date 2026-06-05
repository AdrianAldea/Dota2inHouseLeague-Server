using IHLDota2Bot.Common.DTOs;
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

    public async Task<PlayerEntity?> UpdateSteamIdAsync(ulong userId, string steamId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var player = await context.Players.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (player == null) return null;

        player.SteamId = steamId;
        player.LastSeenAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return player;
    }

    public async Task<bool> DeleteAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var player = await context.Players.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (player == null) return false;

        context.Players.Remove(player);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PlayerEntity?> SetBanAsync(ulong userId, bool banned, string? reason, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var player = await context.Players.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (player == null) return null;

        player.IsBanned = banned;
        player.BanReason = banned ? reason : null;
        player.BannedAt = banned ? DateTime.UtcNow : null;
        await context.SaveChangesAsync(cancellationToken);
        return player;
    }

    public async Task<PlayerStatsResponse?> GetStatsAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Players
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new PlayerStatsResponse
            {
                UserId = p.UserId,
                Username = p.Username,
                SteamId = p.SteamId,
                Rating = p.Rating,
                Wins = p.Wins,
                Losses = p.Losses
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<LeaderboardResponse> GetLeaderboardAsync(int topN, CancellationToken cancellationToken = default)
    {
        if (topN <= 0) topN = 10;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await context.Players
            .AsNoTracking()
            .OrderByDescending(p => p.Rating)
            .ThenByDescending(p => p.Wins)
            .ThenBy(p => p.Losses)
            .ThenBy(p => p.Username)
            .Take(topN)
            .Select(p => new
            {
                p.UserId,
                p.Username,
                p.Rating,
                p.Wins,
                p.Losses
            })
            .ToListAsync(cancellationToken);

        var entries = rows.Select((row, index) => new LeaderboardEntry
        {
            Rank = index + 1,
            UserId = row.UserId,
            Username = row.Username,
            Rating = row.Rating,
            Wins = row.Wins,
            Losses = row.Losses
        }).ToList();

        return new LeaderboardResponse { TopN = topN, Entries = entries };
    }
}
