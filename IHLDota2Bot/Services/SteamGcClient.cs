using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.DTOs;
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
/// <remarks>
/// Threading model: all SteamKit callbacks (OnConnected/OnLoggedOn/OnGCMessage/…)
/// are dispatched serially from the single <c>RunWaitCallbacks</c> pump thread, so
/// they never race each other. The public API (<see cref="StartAsync"/>,
/// <see cref="StopAsync"/>, <see cref="CreateLobbyAsync"/>,
/// <see cref="LeaveActiveLobbyAsync"/>, the <c>Is*</c> getters) is called from other
/// threads (event handlers, API requests). The simple <c>bool</c> state flags shared
/// across those boundaries are <c>volatile</c> so writes on the pump thread are
/// visible to callers and vice-versa.
/// </remarks>
public sealed class SteamGcClient
{
    private const uint DotaAppId = 570;
    private const int LobbyTypeId = 2004;

    // Reconnect backoff: start at 5s and double up to a 2-minute ceiling so a
    // prolonged Steam outage doesn't hammer the CMs while a brief blip still
    // recovers quickly. Reset to 0 on a successful logon.
    private static readonly TimeSpan ReconnectBaseDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromMinutes(2);
    private int reconnectAttempts;

    // Set when Steam rejects our credentials with a non-retryable result
    // (bad password, SteamGuard required, account disabled/banned). Retrying
    // the same secrets can't succeed and risks a lockout, so the reconnect
    // loop stops until the process is restarted with corrected configuration.
    private volatile bool authenticationFailed;

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

    // The app's lobby id for the currently hosted Dota lobby. Unlike
    // activeRoster (cleared the moment everyone is seated and the match
    // launches), this survives until the lobby is left/destroyed so we can
    // correlate a POSTGAME snapshot back to the app's match record.
    private string? hostedAppLobbyId;

    // POSTGAME SO snapshots repeat; this one-shot guard stops us from posting
    // the result / completing the match more than once per hosted lobby.
    private volatile bool matchResultReported;

    // Set whenever we either send a PracticeLobbyCreate or observe a lobby SO
    // snapshot, cleared on intentional leave / disconnect. Used by StopAsync
    // and the host-shutdown hooks to send PracticeLobbyLeave so a crashed or
    // closed server doesn't strand a ghost lobby on the Dota GC.
    private volatile bool hasActiveLobby;

    private volatile bool isRunning;
    private volatile bool isLoggedOn;

    private readonly record struct ActiveRoster(string? AppLobbyId, HashSet<ulong> Expected);

    public event EventHandler<LobbyLaunchedEventArgs>? LobbyLaunched;
    public event EventHandler<MatchCompletedEventArgs>? MatchCompleted;

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

    public async Task StopAsync()
    {
        isRunning = false;

        // Try to leave any hosted lobby before tearing down the socket so the
        // GC reaps it rather than leaving a ghost behind a logged-off host.
        // Best-effort: we don't await GC ack, just give SteamKit a moment to
        // flush the outbound message.
        if (isLoggedOn && hasActiveLobby)
        {
            try
            {
                SendLeaveLobby();
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[{Username}] Failed to send PracticeLobbyLeave during shutdown", username);
            }
        }

        isLoggedOn = false;
        steamClient.Disconnect();
    }

    /// <summary>
    /// Synchronous best-effort lobby cleanup for crash / ProcessExit paths
    /// where there's no time to await. Sends PracticeLobbyLeave and blocks
    /// briefly so the message reaches SteamKit's send buffer before the
    /// process tears down.
    /// </summary>
    public void DestroyActiveLobby()
    {
        if (!isLoggedOn || !hasActiveLobby) return;

        try
        {
            SendLeaveLobby();
            // Block — caller is on the shutdown path and the process is about
            // to exit; using Task.Delay async would race the runtime tearing
            // down the thread pool.
            Thread.Sleep(500);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[{Username}] DestroyActiveLobby failed", username);
        }
    }

    /// <summary>
    /// Admin-triggered teardown of the hosted lobby. Sends PracticeLobbyLeave so
    /// the Dota GC reaps the practice lobby, then yields ~500ms so SteamKit can
    /// flush the outbound frame before the caller continues. Awaitable
    /// counterpart to <see cref="DestroyActiveLobby"/> (which blocks for the
    /// crash / ProcessExit path). Returns true if a leave was actually sent.
    /// </summary>
    public async Task<bool> LeaveActiveLobbyAsync()
    {
        if (!isLoggedOn || !hasActiveLobby)
        {
            logger.LogInformation("[{Username}] LeaveActiveLobbyAsync: no active lobby to leave", username);
            return false;
        }

        SendLeaveLobby();
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        return true;
    }

    private void SendLeaveLobby()
    {
        logger.LogInformation("[{Username}] Sending PracticeLobbyLeave to abandon hosted lobby", username);
        var leaveMsg = new ClientGCMsgProtobuf<CMsgPracticeLobbyLeave>((uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyLeave);
        steamGameCoordinator.Send(leaveMsg, DotaAppId);
        hasActiveLobby = false;
        activeRoster = null;
        hostedAppLobbyId = null;
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
        hostedAppLobbyId = req.AppLobbyId;
        matchResultReported = false;

        logger.LogInformation("[{Username}] Sending CMsgPracticeLobbyCreate for lobby {LobbyName}", username, req.LobbyName);

        var createLobbyMsg = new ClientGCMsgProtobuf<CMsgPracticeLobbyCreate>((uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyCreate);
        createLobbyMsg.Body.lobby_details = new CMsgPracticeLobbySetDetails
        {
            game_name = req.LobbyName,
            pass_key = req.LobbyPassword,
            game_mode = (uint)DOTA_GameMode.DOTA_GAMEMODE_AP,
            server_region = 3
        };

        steamGameCoordinator.Send(createLobbyMsg, DotaAppId);
        hasActiveLobby = true;

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

            case (uint)ESOMsg.k_ESOMsg_Update:
                ProcessSingleObject(callback);
                break;

            case (uint)ESOMsg.k_ESOMsg_UpdateMultiple:
                ProcessMultipleObjects(callback);
                break;

            case (uint)ESOMsg.k_ESOMsg_CacheSubscribed:
                logger.LogDebug("[{Username}] SO cache subscribed", username);
                ProcessCacheSubscribed(callback);
                break;

            case (uint)ESOMsg.k_ESOMsg_Destroy:
                ProcessObjectDestroyed(callback);
                break;

            case (uint)ESOMsg.k_ESOMsg_CacheUnsubscribed:
                ProcessCacheUnsubscribed(callback);
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
        // Let SteamKit parse the GC protobuf header; deserializing GetData()
        // directly feeds the header bytes into the body type and throws
        // "Invalid wire-type".
        var connStatus = new ClientGCMsgProtobuf<CMsgConnectionStatus>(callback.Message).Body;

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

    private void ProcessSingleObject(SteamGameCoordinator.MessageCallback callback)
    {
        try
        {
            // A single SO changed — e.g. one member switching team, or the lobby
            // flipping to POSTGAME. Same header-stripping caveat as the others.
            var soMessage = new ClientGCMsgProtobuf<CMsgSOSingleObject>(callback.Message).Body;

            if (soMessage.type_id == LobbyTypeId && soMessage.object_data != null)
            {
                HandleLobbyUpdate(soMessage.object_data);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Username}] Failed to process SO single object", username);
        }
    }

    private void ProcessMultipleObjects(SteamGameCoordinator.MessageCallback callback)
    {
        try
        {
            // ClientGCMsgProtobuf strips the GC protobuf header. Deserializing
            // callback.Message.GetData() directly throws "Invalid wire-type"
            // because the leading header bytes get parsed as the body.
            var soMessage = new ClientGCMsgProtobuf<CMsgSOMultipleObjects>(callback.Message).Body;

            // A lobby object can show up as freshly added or modified; check both.
            var lobbyData = soMessage.objects_added
                .Concat(soMessage.objects_modified)
                .Where(obj => obj.type_id == LobbyTypeId && obj.object_data != null)
                .Select(obj => obj.object_data)
                .LastOrDefault();

            if (lobbyData != null)
            {
                HandleLobbyUpdate(lobbyData);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Username}] Failed to process SO multiple objects", username);
        }
    }

    private void ProcessCacheSubscribed(SteamGameCoordinator.MessageCallback callback)
    {
        try
        {
            // When the bot creates/hosts a lobby, the full lobby object arrives
            // here in the cache snapshot (not only via UpdateMultiple), so the
            // "all seated -> launch" check has to run on this path too.
            var subscribed = new ClientGCMsgProtobuf<CMsgSOCacheSubscribed>(callback.Message).Body;

            var lobbyData = subscribed.objects
                .Where(o => o.type_id == LobbyTypeId)
                .SelectMany(o => o.object_data)
                .LastOrDefault();

            if (lobbyData != null)
            {
                HandleLobbyUpdate(lobbyData);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Username}] Failed to process SO cache subscribed", username);
        }
    }

    private void ProcessObjectDestroyed(SteamGameCoordinator.MessageCallback callback)
    {
        try
        {
            // k_ESOMsg_Destroy reuses CMsgSOSingleObject (type_id + object_data),
            // same header-stripping caveat as Update. object_data carries the
            // lobby's *final* state, which may be a POSTGAME outcome we never got
            // a plain Update for.
            var soMessage = new ClientGCMsgProtobuf<CMsgSOSingleObject>(callback.Message).Body;
            if (soMessage.type_id != LobbyTypeId) return;

            if (soMessage.object_data is { Length: > 0 })
            {
                using var ms = new MemoryStream(soMessage.object_data);
                var lobby = Serializer.Deserialize<CSODOTALobby>(ms);
                // Last chance to record a decisive result before the lobby is gone.
                if (lobby != null) DetectMatchCompletion(lobby);
            }

            HandleLobbyGone("lobby SO destroyed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Username}] Failed to process SO destroy", username);
        }
    }

    private void ProcessCacheUnsubscribed(SteamGameCoordinator.MessageCallback callback)
    {
        // CacheUnsubscribed means an SO cache we were subscribed to went away. It
        // carries only the owner soid, which we don't need: if we still think we
        // host a lobby, that cache was ours and the lobby is over.
        if (!hasActiveLobby && hostedAppLobbyId is null) return;

        logger.LogInformation("[{Username}] SO cache unsubscribed; treating hosted lobby as gone", username);
        HandleLobbyGone("SO cache unsubscribed");
    }

    /// <summary>
    /// The hosted lobby's shared object is gone (destroyed or unsubscribed).
    /// If a decisive POSTGAME result was already reported, just clears local
    /// state. Otherwise the lobby ended without a scored result (abandon, crash,
    /// everyone left) — raise <see cref="MatchCompleted"/> with a null winner so
    /// the app cancels the match (no rating change), releases the account, and
    /// reopens the queue. One-shot via <c>matchResultReported</c>, so a Destroy
    /// followed by a CacheUnsubscribed (or vice-versa) won't double-fire.
    /// </summary>
    private void HandleLobbyGone(string reason)
    {
        if (!hasActiveLobby && hostedAppLobbyId is null) return;

        var appLobbyId = hostedAppLobbyId;
        var alreadyReported = matchResultReported;

        // Drop local lobby state up front so StopAsync / reconnect don't try to
        // leave a lobby that no longer exists.
        hasActiveLobby = false;
        activeRoster = null;
        hostedAppLobbyId = null;
        matchResultReported = true;

        if (alreadyReported)
        {
            logger.LogInformation("[{Username}] Hosted lobby gone ({Reason}); result already recorded", username, reason);
            return;
        }

        logger.LogWarning(
            "[{Username}] Hosted lobby gone ({Reason}) with no scored result; signalling abandoned match for {AppLobbyId}",
            username, reason, appLobbyId ?? "(none)");

        MatchCompleted?.Invoke(this, new MatchCompletedEventArgs
        {
            Username = username,
            AppLobbyId = appLobbyId,
            DotaLobbyId = 0,
            MatchId = 0,
            WinningTeam = null,
            Outcome = EMatchOutcome.k_EMatchOutcome_Unknown
        });
    }

    private void HandleLobbyUpdate(byte[] objectData)
    {
        using var ms = new MemoryStream(objectData);
        var lobby = Serializer.Deserialize<CSODOTALobby>(ms);
        if (lobby == null) return;

        hasActiveLobby = true;

        // The bot only hosts; it must not occupy one of the 10 team slots.
        // The GC sometimes seats the lobby creator on Radiant by default, so
        // self-correct here: if we see our own account on a team, move it to
        // the unassigned player pool. Runs before the roster check so it also
        // applies to admin/passthrough lobbies.
        EnsureBotIsNotSeated(lobby);

        // Once the GC scores the game the lobby flips to POSTGAME and carries the
        // outcome. Raise the result before the launch/roster bookkeeping so it
        // fires even after activeRoster was cleared at launch.
        DetectMatchCompletion(lobby);

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

        // Per-member team breakdown so we can tell "in the lobby but unassigned"
        // apart from "seated on a team". Only GOOD_GUYS/BAD_GUYS count toward the
        // launch check; players in PLAYER_POOL/spectator/unassigned won't seat.
        if (lobby.all_members.Count > 0)
        {
            logger.LogDebug(
                "[{Username}] Lobby members: {Members}",
                username,
                string.Join(", ", lobby.all_members.Select(m => $"{m.id}=>{m.team}")));
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

    // Maps the GC's scored outcome to the app's winning team. Returns null for
    // anything that isn't a clean Radiant/Dire win (still scoring, abandoned,
    // server crash, never started, etc.).
    private static MatchTeam? OutcomeToWinner(EMatchOutcome outcome) => outcome switch
    {
        EMatchOutcome.k_EMatchOutcome_RadVictory => MatchTeam.Radiant,
        EMatchOutcome.k_EMatchOutcome_DireVictory => MatchTeam.Dire,
        _ => null
    };

    private void DetectMatchCompletion(CSODOTALobby lobby)
    {
        if (matchResultReported) return;
        if (lobby.state != CSODOTALobby.State.POSTGAME) return;

        var outcome = lobby.match_outcome;
        var winner = OutcomeToWinner(outcome);

        // POSTGAME can briefly show Unknown before the GC writes the result.
        // Hold until it's either a decisive win or an explicit not-scored code
        // so we don't post a half-baked result.
        if (winner is null && outcome == EMatchOutcome.k_EMatchOutcome_Unknown) return;

        matchResultReported = true;

        logger.LogInformation(
            "[{Username}] Match completed: LobbyId={LobbyId} MatchId={MatchId} Outcome={Outcome} Winner={Winner}",
            username, lobby.lobby_id, lobby.match_id, outcome,
            winner?.ToString() ?? "none");

        MatchCompleted?.Invoke(this, new MatchCompletedEventArgs
        {
            Username = username,
            AppLobbyId = hostedAppLobbyId,
            DotaLobbyId = lobby.lobby_id,
            MatchId = lobby.match_id,
            WinningTeam = winner,
            Outcome = outcome
        });
    }

    private static bool IsOnTeamSlot(CSODOTALobbyMember member) =>
        member.team == DOTA_GC_TEAM.DOTA_GC_TEAM_GOOD_GUYS
        || member.team == DOTA_GC_TEAM.DOTA_GC_TEAM_BAD_GUYS;

    // Where the bot parks itself so it never consumes a player slot. Swap to
    // DOTA_GC_TEAM_BROADCASTER if you'd rather the bot show up as a caster.
    private const DOTA_GC_TEAM BotParkTeam = DOTA_GC_TEAM.DOTA_GC_TEAM_BROADCASTER;

    private void EnsureBotIsNotSeated(CSODOTALobby lobby)
    {
        var mySteamId = steamClient.SteamID;
        if (mySteamId is null || mySteamId.ConvertToUInt64() == 0) return;

        var me = lobby.all_members.FirstOrDefault(m => m.id == mySteamId.ConvertToUInt64());
        if (me is null || !IsOnTeamSlot(me)) return;

        logger.LogInformation(
            "[{Username}] Bot is seated on {Team}; moving it off the player slot to {Target}",
            username, me.team, BotParkTeam);

        // Strips the bot of its team slot and throws it into "Unassigned"; it
        // stays inside the lobby as the HOST/LEADER.
        var unassignMsg = new ClientGCMsgProtobuf<CMsgPracticeLobbyKickFromTeam>(
            (uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyKickFromTeam);
        unassignMsg.Body.account_id = mySteamId.AccountID;

        steamGameCoordinator.Send(unassignMsg, DotaAppId);
    }

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
        // The GC session is gone, so even if we thought we had a lobby it's no
        // longer ours to leave — drop the flag so a reconnect doesn't try to
        // send PracticeLobbyLeave for a stale lobby.
        hasActiveLobby = false;
        // NB: keep hostedAppLobbyId. If the bot reconnects mid-game and only then
        // sees the POSTGAME snapshot, this is what maps the result back to the
        // app match. It's overwritten on the next CreateLobby and cleared when the
        // lobby SO is destroyed/unsubscribed, so a stale value can't mis-attribute.

        if (!isRunning)
        {
            logger.LogInformation("[{Username}] Disconnected from Steam (clean shutdown)", username);
            return;
        }

        if (authenticationFailed)
        {
            logger.LogError(
                "[{Username}] Not reconnecting: Steam rejected this account's credentials. " +
                "Fix the configured username/password (and SteamGuard, if enabled) and restart.",
                username);
            return;
        }

        // SteamKit's RunWaitCallbacks loop is still pumping (we never set
        // isRunning=false here), so once Connect() succeeds the ConnectedCallback
        // fires again and re-triggers LogOn. Back off exponentially so a sustained
        // outage doesn't spin; the delay resets once we log on successfully.
        var attempt = reconnectAttempts++;
        var delaySeconds = Math.Min(
            ReconnectBaseDelay.TotalSeconds * Math.Pow(2, attempt),
            MaxReconnectDelay.TotalSeconds);
        var delay = TimeSpan.FromSeconds(delaySeconds);

        logger.LogWarning(
            "[{Username}] Disconnected from Steam (UserInitiated={UserInitiated}); reconnecting in {Delay:F0}s (attempt {Attempt})",
            username, callback.UserInitiated, delay.TotalSeconds, attempt + 1);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay);
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

    // Logon failures that won't fix themselves by retrying the same secrets.
    private static bool IsNonRetryableLogonResult(EResult result) => result switch
    {
        EResult.InvalidPassword => true,
        EResult.AccountLogonDenied => true,             // SteamGuard email code required
        EResult.AccountLoginDeniedNeedTwoFactor => true,
        EResult.AccountDisabled => true,
        EResult.Banned => true,
        _ => false
    };

    private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
    {
        isLoggedOn = false;
        ResetGcReady();
        activeRoster = null;
        hasActiveLobby = false;
        // SteamKit will fire a DisconnectedCallback right after — that path
        // owns the reconnect logic. Just record the reason here.
        logger.LogWarning("[{Username}] Logged off Steam: {Result}", username, callback.Result);
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result != EResult.OK)
        {
            logger.LogError("[{Username}] Steam authentication failed: {Result}", username, callback.Result);
            if (IsNonRetryableLogonResult(callback.Result))
            {
                authenticationFailed = true;
                logger.LogError(
                    "[{Username}] Logon result {Result} is non-retryable; disabling reconnect for this account until restart",
                    username, callback.Result);
            }
            return;
        }

        isLoggedOn = true;
        reconnectAttempts = 0;
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

public sealed class MatchCompletedEventArgs : EventArgs
{
    public required string Username { get; init; }
    public string? AppLobbyId { get; init; }
    public ulong DotaLobbyId { get; init; }
    public ulong MatchId { get; init; }

    // Radiant/Dire when the GC scored a decisive winner; null when the match
    // wasn't scored (abandon, server crash, never started, ...).
    public MatchTeam? WinningTeam { get; init; }
    public EMatchOutcome Outcome { get; init; }
}
