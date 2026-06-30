namespace IHLDota2Bot.Services;

/// <summary>
/// The registry of <see cref="ISteamGcClient"/> connections keyed by Steam username.
/// Extracted from <see cref="SteamGcClientPool"/> so consumers (notably
/// <see cref="LobbyPoolManager"/>) bind to the abstraction and can be unit-tested with
/// a substituted pool — mirroring the <see cref="ISteamGcClient"/> seam.
/// </summary>
public interface ISteamGcClientPool
{
    /// <summary>The fallback client — first configured BotAccount, or first PoolAccount if none.</summary>
    ISteamGcClient? Default { get; }

    /// <summary>Snapshot of every registered client, keyed by username.</summary>
    IReadOnlyDictionary<string, ISteamGcClient> All { get; }

    /// <summary>The Steam client reserved for <paramref name="username"/>, or null if none is registered.</summary>
    ISteamGcClient? GetByUsername(string username);

    /// <summary>Registers a Steam account at runtime and starts its connection.</summary>
    Task<(bool Added, string Message)> TryAddAccountAsync(string username, string password);
}
