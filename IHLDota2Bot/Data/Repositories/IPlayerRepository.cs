using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Data.Entities;

namespace IHLDota2Bot.Data.Repositories;

public interface IPlayerRepository
{
    Task<PlayerEntity?> GetByUserIdAsync(ulong userId, CancellationToken cancellationToken = default);
    Task<PlayerEntity?> GetBySteamIdAsync(string steamId, CancellationToken cancellationToken = default);
    Task<List<PlayerEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PlayerEntity> UpsertAsync(PlayerEntity player, CancellationToken cancellationToken = default);
    Task<PlayerEntity?> UpdateSteamIdAsync(ulong userId, string steamId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(ulong userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(ulong userId, CancellationToken cancellationToken = default);
    Task<PlayerEntity?> SetBanAsync(ulong userId, bool banned, string? reason, CancellationToken cancellationToken = default);

    Task<PlayerStatsResponse?> GetStatsAsync(ulong userId, CancellationToken cancellationToken = default);
    Task<LeaderboardResponse> GetLeaderboardAsync(int topN, CancellationToken cancellationToken = default);
}
