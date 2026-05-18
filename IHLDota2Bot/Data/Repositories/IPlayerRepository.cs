using IHLDota2Bot.Data.Entities;

namespace IHLDota2Bot.Data.Repositories;

public interface IPlayerRepository
{
    Task<PlayerEntity?> GetByUserIdAsync(ulong userId, CancellationToken cancellationToken = default);
    Task<PlayerEntity?> GetBySteamIdAsync(string steamId, CancellationToken cancellationToken = default);
    Task<List<PlayerEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PlayerEntity> UpsertAsync(PlayerEntity player, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(ulong userId, CancellationToken cancellationToken = default);
}
