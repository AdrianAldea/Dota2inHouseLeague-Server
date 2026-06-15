using IHLDota2Bot.Common.Requests;

namespace IHLDota2Bot.Services;

/// <summary>
/// The Dota 2 Game Coordinator operations a single Steam bot account exposes to
/// the rest of the app: connection lifecycle, hosting/leaving a practice lobby,
/// inviting players, and the launch/completion events. Implemented by
/// <see cref="SteamGcClient"/>; consumers depend on this interface so the
/// SteamKit2/protobuf details stay isolated behind it.
/// </summary>
public interface ISteamGcClient
{
    string Username { get; }
    bool IsRunning { get; }
    bool IsLoggedOn { get; }

    /// <summary>Completes once the GC handshake (ClientHello → Welcome) is done.</summary>
    Task GcReady { get; }

    event EventHandler<LobbyLaunchedEventArgs>? LobbyLaunched;
    event EventHandler<MatchCompletedEventArgs>? MatchCompleted;

    Task<bool> StartAsync();
    Task StopAsync();

    /// <summary>Synchronous best-effort lobby cleanup for crash / ProcessExit paths.</summary>
    void DestroyActiveLobby();

    /// <summary>Admin-triggered teardown of the hosted lobby. Returns true if a leave was sent.</summary>
    Task<bool> LeaveActiveLobbyAsync();

    Task CreateLobbyAsync(CreateLobbyRequest req);
    void InvitePlayersToLobby(IList<ulong> steamIds);
}
