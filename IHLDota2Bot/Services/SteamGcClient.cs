using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.Requests;
using ProtoBuf;
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
    private const int LobbyTypeId = 2004;
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly string username;
    private readonly string password;
    private readonly DiscordClient discordClient;
    private readonly ILogger logger;

    private readonly SteamKit2.SteamClient steamClient;
    private readonly SteamUser steamUser;
    private readonly SteamFriends steamFriends;
    private readonly SteamGameCoordinator steamGameCoordinator;
    private readonly CallbackManager callbackManager;

    // Signals that the GC handshake (ClientHello -> ClientWelcome) has
    // completed for the current session. Callers awaiting GC operations
    // should await GcReady before sending. Reset on disconnect/logoff.
    private TaskCompletionSource<bool> gcReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // The roster the current lobby is waiting on. When non-null, lobby SO
    // snapshots are checked against Expected; when every SteamID is seated
    // on Radiant/Dire, LaunchLobby fires and this is cleared. One per
    // client because each pool account hosts one lobby at a time.
    private ActiveRoster? activeRoster;

    private bool isRunning;
    private bool isLoggedOn;

    private readonly record struct ActiveRoster(string? AppLobbyId, HashSet<ulong> Expected);

    public event EventHandler<LobbyLaunchedEventArgs>? LobbyLaunched;

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
    public Task GcReady => gcReady.Task;

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
        await gcReady.Task;

        // Set the expected roster *before* sending — an SO snapshot for the
        // new lobby can arrive while we're still awaiting Discord I/O below.
        activeRoster = req.ExpectedSteamIds.Count > 0
            ? new ActiveRoster(req.AppLobbyId, [.. req.ExpectedSteamIds])
            : null;

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
        if (callback.AppID != DotaAppId) return;

        // Strip the high-bit flag from the message ID to match standard Protobuf tracking
        uint cleanMsgType = MsgUtil.GetGCMsg(callback.Message.MsgType);

        switch (cleanMsgType)
        {
            case (uint)EGCBaseClientMsg.k_EMsgGCClientWelcome:
                logger.LogInformation("[{Username}] GC handshake complete (Welcome)", username);
                gcReady.TrySetResult(true);
                break;

            case (uint)EGCBaseClientMsg.k_EMsgGCClientConnectionStatus:
                ProcessConnectionStatus(callback);
                break;

            case (uint)ESOMsg.k_ESOMsg_UpdateMultiple:
                ProcessMultipleObjects(callback);
                break;

            case (uint)ESOMsg.k_ESOMsg_CacheSubscribed:
                logger.LogDebug("[{Username}] SO cache subscribed", username);
                break;

            case (uint)EGCBaseMsg.k_EMsgGCInvitationCreated:
                logger.LogInformation("[{Username}] GC invitation created", username);
                break;

            default:
                logger.LogDebug("[{Username}] GC message: EMsg {EMsg}", username, callback.EMsg);
                break;
        }
    }

    private void ProcessConnectionStatus(SteamGameCoordinator.MessageCallback callback)
    {
        using var ms = new MemoryStream(callback.Message.GetData());
        var connStatus = Serializer.Deserialize<CMsgConnectionStatus>(ms);

        switch (connStatus.status)
        {
            case GCConnectionStatus.GCConnectionStatus_HAVE_SESSION:
                logger.LogInformation("[{Username}] GC session healthy", username);
                gcReady.TrySetResult(true);
                break;

            case GCConnectionStatus.GCConnectionStatus_NO_SESSION_IN_LOGON_QUEUE:
                logger.LogWarning(
                    "[{Username}] GC logon queue: position {QueuePosition}/{QueueSize}, est wait {EstimatedWaitSeconds}s (wait {WaitSeconds}s)",
                    username, connStatus.queue_position, connStatus.queue_size,
                    connStatus.estimated_wait_seconds_remaining, connStatus.wait_seconds);
                break;

            case GCConnectionStatus.GCConnectionStatus_NO_SESSION:
                logger.LogWarning("[{Username}] No GC session; handshake will be retried", username);
                ResetGcReady();
                break;

            case GCConnectionStatus.GCConnectionStatus_GC_GOING_DOWN:
                logger.LogWarning("[{Username}] GC going down; pausing requests", username);
                ResetGcReady();
                break;

            default:
                logger.LogInformation("[{Username}] GC connection status: {Status}", username, connStatus.status);
                break;
        }
    }

    private void ProcessMultipleObjects(SteamGameCoordinator.MessageCallback callback)
    {
        try
        {
            using var ms = new MemoryStream(callback.Message.GetData());
            var soMessage = Serializer.Deserialize<CMsgSOMultipleObjects>(ms);

            foreach (var obj in soMessage.objects_modified)
            {
                if (obj.type_id == LobbyTypeId && obj.object_data != null)
                {
                    HandleLobbyUpdate(obj.object_data);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Username}] Failed to process SO multiple objects", username);
        }
    }

    private void HandleLobbyUpdate(byte[] objectData)
    {
        using var ms = new MemoryStream(objectData);
        var lobby = Serializer.Deserialize<CSODOTALobby>(ms);
        if (lobby == null) return;

        var roster = activeRoster;
        if (roster is null)
        {
            // No expected roster (admin passthrough or no active create).
            // Log snapshot so we at least see lobby activity, but don't auto-launch.
            logger.LogDebug(
                "[{Username}] Lobby snapshot (no roster tracked): LobbyId={LobbyId} Members={MemberCount}",
                username, lobby.lobby_id, lobby.all_members.Count);
            return;
        }

        var seated = lobby.all_members
            .Where(IsOnTeamSlot)
            .Select(m => m.id)
            .ToHashSet();

        var missingCount = roster.Value.Expected.Count - roster.Value.Expected.Intersect(seated).Count();

        logger.LogInformation(
            "[{Username}] Lobby update: LobbyId={LobbyId} Seated={Seated}/{Expected} Missing={Missing}",
            username, lobby.lobby_id, seated.Count, roster.Value.Expected.Count, missingCount);

        if (missingCount > 0) return;

        logger.LogInformation("[{Username}] All expected players seated — launching Dota 2 match", username);

        var args = new LobbyLaunchedEventArgs
        {
            Username = username,
            AppLobbyId = roster.Value.AppLobbyId,
            DotaLobbyId = lobby.lobby_id
        };

        // Clear before raising so a re-entrant snapshot doesn't re-launch.
        activeRoster = null;
        LaunchLobby();
        LobbyLaunched?.Invoke(this, args);
    }

    private static bool IsOnTeamSlot(CSODOTALobbyMember member) =>
        member.team == DOTA_GC_TEAM.DOTA_GC_TEAM_GOOD_GUYS
        || member.team == DOTA_GC_TEAM.DOTA_GC_TEAM_BAD_GUYS;

    private void LaunchLobby()
    {
        var msgLaunch = new ClientGCMsgProtobuf<CMsgPracticeLobbyLaunch>((uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyLaunch);
        steamGameCoordinator.Send(msgLaunch, DotaAppId);
    }

    private void ResetGcReady()
    {
        var existing = gcReady;
        if (!existing.Task.IsCompleted) return;
        gcReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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
        ResetGcReady();
        activeRoster = null;

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
        ResetGcReady();
        activeRoster = null;
        // SteamKit will fire a DisconnectedCallback right after — that path
        // owns the reconnect logic. Just record the reason here.
        logger.LogWarning("[{Username}] Logged off Steam: {Result}", username, callback.Result);
    }

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

        // Order matters: advertise game presence before saying hello to the
        // Dota GC. SteamKit preserves send order on the underlying socket,
        // so no sleep is needed between these — readiness is signalled by
        // the k_EMsgGCClientWelcome callback handled in OnGCMessage.
        var gamePlayed = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
        gamePlayed.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed { game_id = DotaAppId });
        steamClient.Send(gamePlayed);

        var hello = new ClientGCMsgProtobuf<SteamKit2.GC.Dota.Internal.CMsgClientHello>((uint)EGCBaseClientMsg.k_EMsgGCClientHello);
        steamGameCoordinator.Send(hello, DotaAppId);

        logger.LogInformation("[{Username}] Sent ClientGamesPlayed + GC ClientHello; awaiting Welcome", username);
    }
}

public sealed class LobbyLaunchedEventArgs : EventArgs
{
    public required string Username { get; init; }
    public string? AppLobbyId { get; init; }
    public ulong DotaLobbyId { get; init; }
}
