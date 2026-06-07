using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.Requests;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.Dota.Internal;
using SteamKit2.Internal;

namespace IHLDota2Bot.Services;

/// <summary>
/// One SteamKit2 connection for a single Steam account. Owns the callback
/// loop and exposes the Dota 2 GC operations (create lobby, invite players).
///
/// Instances are created and owned by <see cref="SteamGcClientPool"/>, which
/// holds one client per configured bot/pool account so the league can run
/// concurrent Dota lobbies.
/// </summary>
public sealed class SteamGcClient
{
    private const uint DotaAppId = 570;

    private readonly string username;
    private readonly string password;
    private readonly DiscordClient discordClient;
    private readonly ILogger logger;

    private readonly SteamKit2.SteamClient steamClient;
    private readonly SteamUser steamUser;
    private readonly SteamFriends steamFriends;
    private readonly SteamGameCoordinator steamGameCoordinator;
    private readonly CallbackManager callbackManager;

    private bool isRunning;
    private bool isLoggedOn;

    public SteamGcClient(string username, string password, DiscordClient discordClient, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(logger);

        this.username = username;
        this.password = password;
        this.discordClient = discordClient;
        this.logger = logger;

        steamClient = new SteamKit2.SteamClient();
        steamUser = steamClient.GetHandler<SteamUser>()!;
        steamFriends = steamClient.GetHandler<SteamFriends>()!;
        steamGameCoordinator = steamClient.GetHandler<SteamGameCoordinator>()!;

        callbackManager = new CallbackManager(steamClient);
        callbackManager.Subscribe<SteamKit2.SteamClient.ConnectedCallback>(OnConnected);
        callbackManager.Subscribe<SteamKit2.SteamClient.DisconnectedCallback>(OnDisconnected);
        callbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        callbackManager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
        callbackManager.Subscribe<SteamGameCoordinator.MessageCallback>(OnGCMessage);
    }

    public string Username => username;
    public bool IsRunning => isRunning;
    public bool IsLoggedOn => isLoggedOn;

    public Task<bool> StartAsync()
    {
        if (isRunning) return Task.FromResult(true);

        isRunning = true;
        _ = Task.Run(ConnectAndPumpCallbacks);
        return Task.FromResult(true);
    }

    public Task StopAsync()
    {
        isRunning = false;
        isLoggedOn = false;
        steamClient.Disconnect();
        return Task.CompletedTask;
    }

    private void ConnectAndPumpCallbacks()
    {
        logger.LogInformation("[{Username}] Steam connection task starting", username);

        try
        {
            steamClient.Connect();
            logger.LogDebug("[{Username}] steamClient.Connect() returned, entering callback pump", username);
        }
        catch (Exception ex)
        {
            // Don't bail — the loop below will keep RunWaitCallbacks alive, and OnDisconnected
            // (when SteamKit eventually realises) will trigger the reconnect path.
            logger.LogError(ex, "[{Username}] Initial Connect() threw; loop will continue and retry via OnDisconnected", username);
        }

        while (isRunning)
        {
            try
            {
                callbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                // Without this catch, one bad packet would terminate the loop forever
                // and leave isRunning=true (so StartAsync wouldn't even retry).
                logger.LogError(ex, "[{Username}] Exception in RunWaitCallbacks; continuing", username);
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }

        logger.LogInformation("[{Username}] Steam connection task exiting (clean shutdown)", username);
    }

    public async Task CreateLobbyAsync(CreateLobbyRequest req)
    {
        logger.LogInformation("[{Username}] Sending CMsgPracticeLobbyCreate for lobby {LobbyName}", username, req.LobbyName);

        var createLobbyMsg = new ClientGCMsgProtobuf<CMsgPracticeLobbyCreate>((uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyCreate);
        createLobbyMsg.Body.lobby_details = new CMsgPracticeLobbySetDetails
        {
            game_name = req.LobbyName,
            pass_key = req.LobbyPassword,
            game_mode = (uint)DOTA_GameMode.DOTA_GAMEMODE_AP,
            server_region = 2
        };

        steamGameCoordinator.Send(createLobbyMsg, DotaAppId);

        var message = $"[{username}] Lobby created: {req.LobbyName}";
        logger.LogInformation("{Message}", message);
        await discordClient.SendMessageAsync(message);
    }

    public void InvitePlayersToLobby(IList<ulong> steamIds)
    {
        foreach (var steamId in steamIds)
        {
            var inviteMsg = new ClientGCMsgProtobuf<CMsgInviteToLobby>((uint)EGCBaseMsg.k_EMsgGCInviteToLobby);
            inviteMsg.Body.steam_id = steamId;

            logger.LogInformation("[{Username}] Invite sent to SteamID {SteamId}", username, steamId);
            steamGameCoordinator.Send(inviteMsg, DotaAppId);
        }
    }

    private void OnGCMessage(SteamGameCoordinator.MessageCallback callback)
    {
        logger.LogInformation("[{Username}] GC message: AppID {AppId} EMsg {EMsg}", username, callback.AppID, callback.EMsg);
    }

    private void OnConnected(SteamKit2.SteamClient.ConnectedCallback callback)
    {
        logger.LogInformation("[{Username}] Connected to Steam; attempting authentication", username);
        steamUser.LogOn(new SteamUser.LogOnDetails
        {
            Username = username,
            Password = password
        });
    }

    private void OnDisconnected(SteamKit2.SteamClient.DisconnectedCallback callback)
    {
        isLoggedOn = false;

        if (!isRunning)
        {
            logger.LogInformation("[{Username}] Disconnected from Steam (clean shutdown)", username);
            return;
        }

        // Reconnect with a short fixed backoff. SteamKit's RunWaitCallbacks loop
        // is still pumping (we never set isRunning=false here) so when Connect()
        // succeeds the ConnectedCallback will fire again and re-trigger LogOn.
        logger.LogWarning(
            "[{Username}] Disconnected from Steam (UserInitiated={UserInitiated}); reconnecting in {Delay}s",
            username, callback.UserInitiated, ReconnectDelay.TotalSeconds);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ReconnectDelay);
                if (!isRunning) return;
                logger.LogInformation("[{Username}] Reconnecting to Steam", username);
                steamClient.Connect();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[{Username}] Reconnect attempt threw", username);
            }
        });
    }

    private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        isLoggedOn = false;
        // SteamKit will fire a DisconnectedCallback right after — that path
        // owns the reconnect logic. Just record the reason here.
        logger.LogWarning("[{Username}] Logged off Steam: {Result}", username, callback.Result);
    }

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result != EResult.OK)
        {
            logger.LogError("[{Username}] Steam authentication failed: {Result}", username, callback.Result);
            return;
        }

        isLoggedOn = true;
        logger.LogInformation("[{Username}] Steam authentication successful", username);
        steamFriends.SetPersonaState(EPersonaState.Online);

        var hello = new ClientGCMsgProtobuf<SteamKit2.GC.Dota.Internal.CMsgClientHello>((uint)EGCBaseClientMsg.k_EMsgGCClientHello);
        steamGameCoordinator.Send(hello, DotaAppId);

        var gamePlayed = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
        gamePlayed.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed { game_id = DotaAppId });
        steamClient.Send(gamePlayed);

        logger.LogInformation("[{Username}] Notified Steam that we are 'playing' Dota 2", username);
    }
}
