using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.Requests;
using ProtoBuf;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.Dota.Internal;
using SteamKit2.Internal;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;

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
    private readonly string username;
    private readonly string password;
    private readonly DiscordClient discordClient;
    private readonly ILogger logger;

    private readonly SteamKit2.SteamClient steamClient;
    private readonly SteamUser steamUser;
    private readonly SteamFriends steamFriends;
    private readonly SteamGameCoordinator steamGameCoordinator;
    private readonly CallbackManager callbackManager;

    // Define a delegate signature for processing structural bytes
    private delegate void GcMessageAction(IPacketGCMsg packetMsg);
    private Dictionary<uint, GcMessageAction> gcMessageMap;

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

        InitializeGcRouter();
    }

    private void InitializeGcRouter()
    {
        gcMessageMap = new Dictionary<uint, GcMessageAction>
        {
            // Map the exact message ID integer to its specific parsing method
            // (Note: Replace numerical IDs with actual compiled Protobuf Enum values if available)
            { 4004, HandleClientWelcome }, // EMsgGCClientWelcome
            { 4005, HandleClientConnectionStatus }, // EMsgGCClientConnectionStatus
            { 7027, HandlePracticeLobbySnapshot } // EMsgGCPracticeLobbySnapshot (example)
        };
    }

    public string Username => username;
    public bool IsRunning => isRunning;
    public bool IsLoggedOn => isLoggedOn;
    public uint DOTA_APP_ID => 570;


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

        steamGameCoordinator.Send(createLobbyMsg, DOTA_APP_ID);

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
            steamGameCoordinator.Send(inviteMsg, DOTA_APP_ID);
        }
    }

    private void OnGCMessage(SteamGameCoordinator.MessageCallback callback)
    {
        if (callback.AppID != DOTA_APP_ID) return;

        // Strip the high-bit flag from the message ID to match standard Protobuf tracking
        uint cleanMsgType = MsgUtil.GetGCMsg(callback.Message.MsgType);

        switch (cleanMsgType)
        {
            case (uint)ESOMsg.k_ESOMsg_UpdateMultiple:
                logger.LogInformation("OnGCMessage: ESOMsg.k_ESOMsg_UpdateMultiple {ESOMsg.k_ESOMsg_UpdateMultiple}", callback.EMsg);
                ProcessMultipleObjects(callback);
                break;

            case (uint)EGCBaseClientMsg.k_EMsgGCClientConnectionStatus:
                logger.LogInformation("OnGCMessage: EGCBaseClientMsg.k_EMsgGCClientConnectionStatus {EGCBaseClientMsg.k_EMsgGCClientConnectionStatus}", callback.EMsg);
                ProcessConnectionStatus(callback);
                break;

            case (uint)ESOMsg.k_ESOMsg_CacheSubscribed:
                logger.LogInformation("OnGCMessage: ESOMsg.k_ESOMsg_CacheSubscribed {ESOMsg.k_ESOMsg_CacheSubscribed}", callback.EMsg);
                break;

            case (uint)EGCBaseClientMsg.k_EMsgGCClientWelcome:
                logger.LogInformation("OnGCMessage: EGCBaseClientMsg.k_EMsgGCClientWelcome {EGCBaseClientMsg.k_EMsgGCClientWelcome}", callback.EMsg);
                break;

            case (uint)EGCBaseMsg.k_EMsgGCInvitationCreated:
                logger.LogInformation("OnGCMessage: EGCBaseMsg.k_EMsgGCInvitationCreated {EGCBaseMsg.k_EMsgGCInvitationCreated}", callback.EMsg);
                break;
        }

        logger.LogInformation("[{Username}] GC message: AppID {AppId} EMsg {EMsg}", username, callback.AppID, callback.EMsg);
    }

    private void ProcessConnectionStatus(SteamGameCoordinator.MessageCallback callback)
    {
        // Deserialize using protobuf-net
        var connStatus = ProtoBuf.Serializer.Deserialize<CMsgConnectionStatus>(new MemoryStream(callback.Message.GetData()));

        logger.LogDebug("🌐 [GC Connection Status Update]: Status={Status}", connStatus.status);

        switch (connStatus.status)
        {
            case GCConnectionStatus.GCConnectionStatus_HAVE_SESSION:
                logger.LogInformation("✅ Bot session is healthy and active. Safe to create lobbies!");
                break;

            case GCConnectionStatus.GCConnectionStatus_NO_SESSION_IN_LOGON_QUEUE:
                logger.LogInformation("⏳ Bot is stuck in Valve's logon queue.");
                logger.LogInformation($"Queue Position: {connStatus.queue_position} / {connStatus.queue_size}");
                logger.LogInformation($"Est. Wait Time: {connStatus.estimated_wait_seconds_remaining} ({connStatus.wait_seconds}s)");
                break;

            case GCConnectionStatus.GCConnectionStatus_NO_SESSION:
                logger.LogInformation("❌ Disconnected from the Game Coordinator. Retrying ClientHello handshake...");
                // Trigger your custom reconnect/Hello mechanism here
                break;

            case GCConnectionStatus.GCConnectionStatus_GC_GOING_DOWN:
                logger.LogInformation("⚠️ Valve's Dota 2 GC is restarting or updating. Pausing requests.");
                break;
        }
    }

    public void ProcessMultipleObjects(SteamGameCoordinator.MessageCallback callback)
    {
        int LOBBY_TYPE_ID = 2004;

        try
        {
            var soMessage = Serializer.Deserialize<CMsgSOMultipleObjects>(new MemoryStream(callback.Message.GetData()));


            foreach (var obj in soMessage.objects_modified)
            {
                if (obj.type_id == LOBBY_TYPE_ID && obj.object_data != null)
                {
                    //HandleLobbyUpdate(obj.object_data);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] Eroare în ProcessMultipleObjects: {ex.Message}");
        }
    }

    private void HandleLobbyUpdate(byte[] objectData)
    {
        using var ms = new MemoryStream(objectData);

        CSODOTALobby lobby = Serializer.Deserialize<CSODOTALobby>(ms);

        if (lobby == null) return;

        logger.LogInformation("[HandleLobbyUpdate] Lobby ID: {lobby.lobby_id} | Players: {lobby.all_members.Count}", lobby.lobby_id, lobby.all_members.Count);

        bool validateResult = ValidatePlayers(lobby);

        if (validateResult)
        {
            logger.LogInformation("[HandleLobbyUpdate] Launching Dota 2 match...");
            LaunchLobby();
        }
    }

    private bool ValidatePlayers(CSODOTALobby lobby) =>
        lobby.all_members.Count == 2;

    private void LaunchLobby()
    {
        var msgLaunch = new ClientGCMsgProtobuf<CMsgPracticeLobbyLaunch>((uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyLaunch);
        //msgLaunch.Body.client_version = 570;
        steamGameCoordinator.Send(msgLaunch, DOTA_APP_ID);
    }

    private (string, T) DeserializeToStringAndObject<T>(byte[] param)
    {
        var jsonString = Encoding.UTF8.GetString(param);

        var obj = JsonSerializer.Deserialize<T>(jsonString)!;
        return (jsonString, obj);
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

        var gamePlayed = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
        gamePlayed.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed { game_id = DOTA_APP_ID });
        steamClient.Send(gamePlayed);
        logger.LogInformation("[{Username}] Notified Steam that we are 'playing' Dota 2", username);

        Thread.Sleep(1000); // Short delay to ensure Steam processes the "playing" status before we send ClientHello

        var hello = new ClientGCMsgProtobuf<SteamKit2.GC.Dota.Internal.CMsgClientHello>((uint)EGCBaseClientMsg.k_EMsgGCClientHello);
        steamGameCoordinator.Send(hello, DOTA_APP_ID);

        logger.LogInformation("[{Username}] Sent GC ClientHello", username);

        Thread.Sleep(1000); // Short delay to allow GC handshake to complete and trigger connection status updates before we attempt lobby operations
    }
}
