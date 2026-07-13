using IHLDota2Bot.Data.Entities;
using IHLDota2Bot.Data.Repositories;
using Moq;

namespace IHLDota2Bot.Tests.TestSupport;

/// <summary>
/// Shared <see cref="IPlayerRepository"/> stubs for tests that construct
/// <see cref="LobbyPoolManager"/> directly. The manager's lobby-ready path calls
/// <c>GetByUserIdsAsync</c> to balance teams by rating, so the stub must return a
/// (possibly empty) list — an unconfigured mock would return null and fail the flow.
/// </summary>
public static class PlayerRepositoryStubs
{
    /// <summary>
    /// Batch lookup returns no rows: every lobby member falls back to the
    /// default rating during team balancing.
    /// </summary>
    public static IPlayerRepository Empty() => WithPlayers();

    /// <summary>Batch lookup returns exactly the given players.</summary>
    public static IPlayerRepository WithPlayers(params PlayerEntity[] players) =>
        Mock.Of<IPlayerRepository>(r =>
            r.GetByUserIdsAsync(
                It.IsAny<IReadOnlyCollection<ulong>>(),
                It.IsAny<CancellationToken>()
            ) == Task.FromResult(players.ToList())
        );
}
