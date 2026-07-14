using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Data.Repositories;
using IHLDota2Bot.Services;
using IHLDota2Bot.Telemetry;
using Microsoft.Extensions.Options;

namespace IHLDota2Bot;

/// <summary>
/// Main orchestrator for the pool system.
/// </summary>
public class LobbyPoolManager : IHostedService
{
    private readonly SteamAccountPool accountPool;
    private readonly LobbyQueue lobbyQueue;
    private readonly DiscordClient discordClient;
    private readonly IMatchRepository matchRepository;
    private readonly IPlayerRepository playerRepository;
    private readonly ISteamGcClientPool steamGcPool;
    private readonly GameMetrics gameMetrics;
    private readonly ILogger<LobbyPoolManager> logger;
    private readonly SteamOptions steamOptions;
    private readonly Dictionary<string, LobbySession> activeSessions = new();
    private readonly Lock lockObject = new();

    public event EventHandler<GameReadyEventArgs>? GameReady;
    public event EventHandler<GameStartedEventArgs>? GameStarted;
    public event EventHandler<GameFinishedEventArgs>? GameFinished;

    public LobbyPoolManager(
        SteamAccountPool accountPool,
        LobbyQueue lobbyQueue,
        DiscordClient discordClient,
        IMatchRepository matchRepository,
        IPlayerRepository playerRepository,
        ISteamGcClientPool steamGcPool,
        GameMetrics gameMetrics,
        IOptions<SteamOptions> steamOptions,
        ILogger<LobbyPoolManager> logger
    )
    {
        ArgumentNullException.ThrowIfNull(accountPool);
        ArgumentNullException.ThrowIfNull(lobbyQueue);
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(matchRepository);
        ArgumentNullException.ThrowIfNull(playerRepository);
        ArgumentNullException.ThrowIfNull(steamGcPool);
        ArgumentNullException.ThrowIfNull(gameMetrics);
        ArgumentNullException.ThrowIfNull(steamOptions);
        ArgumentNullException.ThrowIfNull(logger);

        this.accountPool = accountPool;
        this.lobbyQueue = lobbyQueue;
        this.discordClient = discordClient;
        this.matchRepository = matchRepository;
        this.playerRepository = playerRepository;
        this.steamGcPool = steamGcPool;
        this.gameMetrics = gameMetrics;
        this.steamOptions = steamOptions.Value;
        this.logger = logger;

        // Expose live queue depth to the observable gauge (snapshotted under the
        // queue's lock via GetStats).
        gameMetrics.SetQueueDepthProvider(() => lobbyQueue.GetStats().CurrentLobbyPlayerCount);

        // LobbyQueue is pure domain. We own the Discord-facing translation of its events.
        this.lobbyQueue.LobbyReady += OnLobbyReady;
        this.lobbyQueue.LobbyCreated += OnLobbyCreated;
        this.lobbyQueue.PlayerJoined += OnPlayerJoined;
        this.lobbyQueue.PlayerLeft += OnPlayerLeft;
        this.lobbyQueue.PlayerReady += OnPlayerReady;
        this.lobbyQueue.PlayerUnready += OnPlayerUnready;
        this.lobbyQueue.PlayerKicked += OnPlayerKicked;
        this.lobbyQueue.LobbyReset += OnLobbyReset;
        this.lobbyQueue.LobbyFinished += OnLobbyFinished;
        this.lobbyQueue.LobbyFull += OnLobbyFull;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var account in steamOptions.PoolAccounts)
        {
            await accountPool.RegisterAccountAsync(account.Username, account.Password);
        }

        await ReconcileStaleInProgressMatchesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// On startup, the in-memory <c>activeSessions</c> dictionary is empty —
    /// any previous run's state was lost. The DB doesn't know that, so matches
    /// the previous process started but never finished sit as <c>InProgress</c>
    /// forever. Walk the DB once, cancel anything older than the configured
    /// threshold, and log what we cleaned up. Without this, the affected
    /// players' lobbies would be stuck and !report wouldn't find a sensible
    /// match to close.
    /// </summary>
    private async Task ReconcileStaleInProgressMatchesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var olderThan = DateTime.UtcNow - LeagueConstants.StaleMatchThreshold;
            var stale = await matchRepository.GetStaleInProgressMatchesAsync(
                olderThan,
                cancellationToken
            );
            if (stale.Count == 0)
            {
                logger.LogInformation("Reconciliation: no stale InProgress matches found");
                return;
            }

            foreach (var match in stale)
            {
                var ok = await matchRepository.CancelMatchAsync(match.Id, cancellationToken);
                if (ok)
                {
                    logger.LogWarning(
                        "Reconciliation: cancelled stale InProgress match {MatchId} (started {Started:o}, age {AgeHours:F1}h)",
                        match.Id,
                        match.CreatedAt,
                        (DateTime.UtcNow - match.CreatedAt).TotalHours
                    );
                }
            }

            logger.LogInformation("Reconciliation: processed {Count} stale match(es)", stale.Count);
        }
        catch (Exception ex)
        {
            // Reconciliation is best-effort. If it fails (DB unavailable on boot),
            // log and continue — the rest of the bot still starts.
            logger.LogError(ex, "Reconciliation pass failed; continuing startup");
        }
    }

    public AccountPoolStats GetAccountPoolStats() => accountPool.GetStats();

    public List<SteamAccount> GetSteamAccounts() => accountPool.GetAllAccounts();

    public int GetAvailableAccountCount() => accountPool.GetAvailableAccountCount();

    public Task<QueueOperationResponse> JoinQueueAsync(
        ulong userId,
        string username,
        string steamId
    ) => lobbyQueue.JoinQueueAsync(userId, username, steamId);

    public Task<QueueOperationResponse> LeaveQueueAsync(ulong userId) =>
        lobbyQueue.LeaveQueueAsync(userId);

    public Task<QueueOperationResponse> MarkPlayerReadyAsync(ulong userId) =>
        lobbyQueue.MarkPlayerReadyAsync(userId);

    public Task<QueueOperationResponse> MarkPlayerUnreadyAsync(ulong userId) =>
        lobbyQueue.MarkPlayerUnreadyAsync(userId);

    public QueueStatusResponse GetQueueStatus() => lobbyQueue.GetQueueStatus();

    public bool RemovePlayerCompletely(ulong userId) => lobbyQueue.RemovePlayerCompletely(userId);

    public bool UpdatePlayerSteamId(ulong userId, string steamId) =>
        lobbyQueue.UpdatePlayerSteamId(userId, steamId);

    public Task<QueueOperationResponse> KickFromLobbyAsync(ulong userId) =>
        lobbyQueue.KickFromLobbyAsync(userId);

    public Task<QueueOperationResponse> ForceLobbyReadyAsync() => lobbyQueue.ForceLobbyReadyAsync();

    public Task<QueueOperationResponse> ResetCurrentLobbyAsync() =>
        lobbyQueue.ResetCurrentLobbyAsync();

    public async Task<QueueOperationResponse> AddSteamAccountAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return new QueueOperationResponse
            {
                Success = false,
                Message = "Username and password are required.",
            };

        if (accountPool.GetAccountByUsername(username) != null)
            return new QueueOperationResponse
            {
                Success = false,
                Message = $"Account {username} already exists.",
            };

        // Register the Steam connection FIRST. If we don't have a real Steam
        // client for this account, reserving it from the bookkeeping pool later
        // would silently fail to create a Dota lobby. Better to refuse the add
        // outright than leave a half-wired account behind.
        var (gcAdded, gcMessage) = await steamGcPool.TryAddAccountAsync(username, password);
        if (!gcAdded)
        {
            logger.LogWarning(
                "Refusing AddSteamAccount({Username}): SteamGcClientPool refused — {Message}",
                username,
                gcMessage
            );
            return new QueueOperationResponse { Success = false, Message = gcMessage };
        }

        await accountPool.RegisterAccountAsync(username, password);
        return new QueueOperationResponse
        {
            Success = true,
            Message = $"Steam account {username} registered and connecting.",
        };
    }

    public PoolStatusResponse GetPoolStatusResponse()
    {
        var stats = accountPool.GetStats();
        return new PoolStatusResponse
        {
            Total = stats.TotalAccounts,
            Ready = stats.ReadyAccounts,
            InUse = stats.InUseAccounts,
            OnCooldown = stats.OnCooldownAccounts,
            Accounts = stats
                .Accounts.Select(a => new PoolAccountSummary
                {
                    Username = a.Username,
                    State = (PoolAccountState)a.State,
                    CurrentLobbyId = a.CurrentLobbyId,
                    LastUsedAt = a.LastUsedAt,
                })
                .ToList(),
        };
    }

    public Lobby GetCurrentLobby() => lobbyQueue.GetCurrentLobby();

    public QueueStats GetQueueStats() => lobbyQueue.GetStats();

    public List<Player> GetRegisteredPlayers() => lobbyQueue.GetRegisteredPlayers();

    private void OnLobbyReady(object? sender, LobbyReadyEventArgs e) =>
        RunHandlerSafely(nameof(OnLobbyReady), () => HandleLobbyReadyAsync(e));

    private async Task HandleLobbyReadyAsync(LobbyReadyEventArgs e)
    {
        try
        {
            var lobby = e.Lobby;
            var account = await accountPool.GetAvailableAccountAsync();

            if (account == null)
            {
                var message = LobbyMessages.NoAvailableAccounts(lobby.Id);
                logger.LogWarning("{Message}", message);
                await discordClient.SendMessageAsync(message);
                return;
            }

            LobbySession session;
            lock (lockObject)
            {
                session = new LobbySession
                {
                    LobbyId = lobby.Id,
                    AccountId = account.Id,
                    Players = lobby.Players.ToList(),
                    State = LobbySessionState.Ready,
                    StartedAt = DateTime.UtcNow,
                };
                activeSessions[lobby.Id] = session;
                lobby.SteamAccountId = account.Id;
            }

            await accountPool.MarkAccountInUseAsync(account.Id, lobby.Id);

            // The DB write is the critical sync point. If we can't persist the match,
            // every later step is moot — players couldn't !report it, ELO wouldn't
            // apply, and the bookkeeping pool would leak this account forever. Roll
            // the in-memory reservation back so the next try (or the next 10
            // players) gets a clean slate.
            bool matchPersisted = false;
            try
            {
                // Balance teams by rating rather than randomly. Ratings live in the
                // DB (the in-memory queue Player doesn't carry one); players without
                // a row fall back to the default rating. Shuffle before balancing so
                // rating ties (everyone starts at the default) don't produce the same
                // teams every match — TeamBalancer is stable within equal ratings.
                var ratings = (
                    await playerRepository.GetByUserIdsAsync(
                        lobby.Players.Select(p => p.UserId).ToList()
                    )
                ).ToDictionary(p => p.UserId, p => p.Rating);

                var teamAssignments = TeamBalancer.Balance(
                    lobby
                        .Players.OrderBy(_ => Guid.NewGuid())
                        .Select(p =>
                            (
                                p.UserId,
                                ratings.GetValueOrDefault(p.UserId, LeagueConstants.DefaultRating)
                            )
                        )
                        .ToList()
                );

                var match = await matchRepository.CreateMatchAsync(lobby.Id, teamAssignments);
                logger.LogInformation(
                    "Match {MatchId} created for lobby {LobbyId}",
                    match.Id,
                    lobby.Id
                );
                matchPersisted = true;
                gameMetrics.RecordMatchCreated();
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to persist match for lobby {LobbyId}; rolling back reservation",
                    lobby.Id
                );
            }

            if (!matchPersisted)
            {
                lock (lockObject)
                {
                    activeSessions.Remove(lobby.Id);
                    lobby.SteamAccountId = null;
                }
                await accountPool.ReleaseAccountAsync(account.Id);
                await SafeBroadcastAsync(LobbyMessages.PersistFailed(lobby.Id, account.Username));
                return;
            }

            // Tell Steam to spin up the actual Dota 2 practice lobby and invite the 10 players.
            // The Steam GC operations run on the *specific* SteamGcClient for the reserved
            // pool account, so multiple concurrent lobbies can co-exist on distinct bots.
            // Name/password are derived from the lobby id (LobbyNaming) so the
            // player-facing /me/match endpoint can reproduce them without persistence.
            var lobbyName = LobbyNaming.For(lobby.Id);
            var lobbyPassword = LobbyNaming.For(lobby.Id);

            var botClient = steamGcPool.GetByUsername(account.Username);
            if (botClient == null)
            {
                logger.LogError(
                    "Lobby {LobbyId} reserved pool account {Username}, but no SteamGcClient is registered for that name. "
                        + "Add the account via Steam:PoolAccounts in configuration, or use !add_account at runtime.",
                    lobby.Id,
                    account.Username
                );
            }
            else if (!botClient.IsLoggedOn)
            {
                logger.LogWarning(
                    "Lobby {LobbyId} reserved pool account {Username}, but its Steam client isn't logged on yet. "
                        + "Skipping CreateLobby / invites. Match still persisted; admin can invoke /admin/game/finished/{LobbyId} to release the account.",
                    lobby.Id,
                    account.Username,
                    lobby.Id
                );
            }
            else
            {
                // Subscribe before CreateLobbyAsync — the GC snapshot can land
                // immediately. Handlers self-detach once they fire for this lobby.
                // Hoisted out of the try so the catch can detach them on rollback.
                var lobbyId = lobby.Id;
                EventHandler<LobbyLaunchedEventArgs>? handler = null;
                EventHandler<MatchCompletedEventArgs>? completedHandler = null;
                try
                {
                    var steamIds = lobby
                        .Players.Select(p => ulong.TryParse(p.SteamId, out var id) ? id : 0UL)
                        .Where(id => id > 0)
                        .ToList();

                    if (steamIds.Count == 0)
                    {
                        logger.LogWarning(
                            "Lobby {LobbyId} ready but no parsable Steam IDs among its players; auto-launch disabled",
                            lobby.Id
                        );
                    }

                    handler = async (_, args) =>
                    {
                        if (args.AppLobbyId != lobbyId)
                            return;
                        botClient.LobbyLaunched -= handler;
                        logger.LogInformation(
                            "Lobby {LobbyId} launched in Dota (DotaLobbyId={DotaLobbyId}); flipping to InProgress",
                            lobbyId,
                            args.DotaLobbyId
                        );
                        try
                        {
                            await NotifyGameStartedAsync(lobbyId);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(
                                ex,
                                "Failed to flip game-started state for lobby {LobbyId}",
                                lobbyId
                            );
                        }
                    };
                    botClient.LobbyLaunched += handler;

                    // The GC tells us the result when the match reaches POSTGAME.
                    // Subscribe a self-detaching handler so the win is recorded and
                    // posted to Discord without anyone having to !report it.
                    completedHandler = async (_, args) =>
                    {
                        if (args.AppLobbyId != lobbyId)
                            return;
                        botClient.MatchCompleted -= completedHandler;
                        try
                        {
                            await HandleMatchCompletedAsync(lobbyId, args);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(
                                ex,
                                "Failed to handle match completion for lobby {LobbyId}",
                                lobbyId
                            );
                        }
                    };
                    botClient.MatchCompleted += completedHandler;

                    await botClient.CreateLobbyAsync(
                        new CreateLobbyRequest
                        {
                            AuthorId = account.Id,
                            LobbyName = lobbyName,
                            LobbyPassword = lobbyPassword,
                            ExpectedSteamIds = steamIds,
                            AppLobbyId = lobby.Id,
                        }
                    );

                    if (steamIds.Count > 0)
                    {
                        botClient.InvitePlayersToLobby(steamIds);
                    }
                }
                catch (Exception ex)
                {
                    // The Steam GC actively failed to stand up the Dota lobby, so — unlike
                    // the bot-offline branches above, which deliberately keep the match for
                    // an admin to finish — there is definitively no game for these players.
                    // Don't announce a lobby that doesn't exist and don't leave the match
                    // InProgress with the account reserved: detach the now-stale launch/
                    // complete handlers and run the compensating rollback so the DB and
                    // Steam agree and the players can re-queue.
                    logger.LogError(
                        ex,
                        "Failed to create Dota lobby / send invites for {LobbyId}; rolling back reservation",
                        lobby.Id
                    );
                    if (handler != null)
                        botClient.LobbyLaunched -= handler;
                    if (completedHandler != null)
                        botClient.MatchCompleted -= completedHandler;
                    await RollbackLaunchAsync(lobby, account, session);
                    return;
                }
            }

            var readyMessage = LobbyMessages.LobbyReady(
                lobby.Id,
                lobbyName,
                lobbyPassword,
                account.Username
            );
            logger.LogInformation("{Message}", readyMessage);
            await discordClient.SendMessageAsync(readyMessage);

            GameReady?.Invoke(
                this,
                new GameReadyEventArgs
                {
                    Lobby = lobby,
                    Account = account,
                    Session = session,
                }
            );

            // InProgress flip happens when SteamGcClient.LobbyLaunched fires
            // (i.e. all expected players have seated on team slots and the
            // bot has sent CMsgGCPracticeLobbyLaunch). The handler is wired
            // above, just before CreateLobbyAsync.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling LobbyReady event");
        }
    }

    /// <summary>
    /// Compensating rollback for the window between persisting a match and the Steam GC
    /// actually creating the Dota lobby. The match was committed and the account reserved,
    /// but the GC call threw — so there is no lobby. Cancel the DB match (no rating change),
    /// release the pool account, drop the in-memory session, and tell the channel so the
    /// players can re-queue. This closes the DB↔Steam consistency gap that would otherwise
    /// leave an orphaned InProgress match until the next startup's stale reconciliation.
    /// </summary>
    private async Task RollbackLaunchAsync(Lobby lobby, SteamAccount account, LobbySession session)
    {
        try
        {
            // Null winner => Cancelled, no rating change; no-op if already settled.
            var result = await matchRepository.CompleteMatchByLobbyAsync(
                lobby.Id,
                winningTeam: null
            );
            logger.LogInformation("RollbackLaunchAsync: {Message}", result.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "RollbackLaunchAsync: failed to cancel match for lobby {LobbyId}",
                lobby.Id
            );
        }

        lock (lockObject)
        {
            activeSessions.Remove(lobby.Id);
            lobby.SteamAccountId = null;
        }

        try
        {
            await accountPool.ReleaseAccountAsync(account.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "RollbackLaunchAsync: failed to release account {AccountId} for lobby {LobbyId}",
                account.Id,
                lobby.Id
            );
        }

        // The match was created then immediately cancelled — record the finish so the
        // lobby→finish lifecycle metric stays balanced against RecordMatchCreated.
        gameMetrics.RecordMatchFinished(
            GameMetrics.OutcomeCancelled,
            DateTime.UtcNow - session.StartedAt
        );

        await SafeBroadcastAsync(LobbyMessages.LaunchFailed(lobby.Id, account.Username));
    }

    private void OnLobbyCreated(object? sender, LobbyCreatedEventArgs e) =>
        RunHandlerSafely(
            nameof(OnLobbyCreated),
            () =>
            {
                gameMetrics.RecordLobbyCreated();
                return SafeBroadcastAsync(
                    LobbyMessages.LobbyCreated(e.Lobby.Id, e.Lobby.Players.Count)
                );
            }
        );

    private void OnPlayerJoined(object? sender, PlayerJoinedEventArgs e) =>
        RunHandlerSafely(
            nameof(OnPlayerJoined),
            () =>
                SafeBroadcastAsync(
                    LobbyMessages.PlayerJoined(
                        e.Player.Username,
                        e.Lobby.Players.Count,
                        lobbyQueue.PlayersPerLobby
                    )
                )
        );

    private void OnPlayerLeft(object? sender, PlayerLeftEventArgs e) =>
        RunHandlerSafely(
            nameof(OnPlayerLeft),
            () =>
                SafeBroadcastAsync(
                    LobbyMessages.PlayerLeft(
                        e.Player.Username,
                        e.Lobby.Players.Count,
                        lobbyQueue.PlayersPerLobby
                    )
                )
        );

    private void OnPlayerReady(object? sender, PlayerReadyEventArgs e) =>
        RunHandlerSafely(
            nameof(OnPlayerReady),
            () =>
            {
                var lobby = lobbyQueue.GetCurrentLobby();
                return SafeBroadcastAsync(
                    LobbyMessages.PlayerReady(e.Player.Username, lobby.ReadyCount, lobby.TotalCount)
                );
            }
        );

    private void OnPlayerUnready(object? sender, PlayerUnreadyEventArgs e) =>
        RunHandlerSafely(
            nameof(OnPlayerUnready),
            () => SafeBroadcastAsync(LobbyMessages.PlayerUnready(e.Player.Username))
        );

    private void OnPlayerKicked(object? sender, PlayerKickedEventArgs e) =>
        RunHandlerSafely(
            nameof(OnPlayerKicked),
            () =>
                SafeBroadcastAsync(
                    LobbyMessages.PlayerKicked(
                        e.Player.Username,
                        e.Lobby.Players.Count,
                        lobbyQueue.PlayersPerLobby
                    )
                )
        );

    private void OnLobbyReset(object? sender, LobbyResetEventArgs e) =>
        RunHandlerSafely(
            nameof(OnLobbyReset),
            () => SafeBroadcastAsync(LobbyMessages.LobbyReset(e.RemovedPlayerCount))
        );

    private void OnLobbyFinished(object? sender, LobbyFinishedEventArgs e) =>
        RunHandlerSafely(
            nameof(OnLobbyFinished),
            () => SafeBroadcastAsync(LobbyMessages.LobbyFinished(e.Lobby.Id))
        );

    private void OnLobbyFull(object? sender, LobbyFullEventArgs e) =>
        RunHandlerSafely(
            nameof(OnLobbyFull),
            () =>
                SafeBroadcastAsync(
                    LobbyMessages.LobbyFull(
                        e.Lobby.Id,
                        e.Lobby.Players.Count,
                        lobbyQueue.PlayersPerLobby
                    )
                )
        );

    /// <summary>
    /// Bridges synchronous <see cref="EventHandler{TEventArgs}"/> callbacks to async
    /// work without using <c>async void</c>. The action runs synchronously up to its
    /// first await (matching the previous handlers' behaviour, so a raiser holding the
    /// queue lock isn't blocked), but any fault — before or after that first await — is
    /// observed and logged here instead of being thrown on a pool thread with no one to
    /// catch it, which would tear down the process.
    /// </summary>
    private void RunHandlerSafely(string handlerName, Func<Task> action) =>
        _ = ObserveHandlerAsync(handlerName, action);

    private async Task ObserveHandlerAsync(string handlerName, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unhandled exception in lobby event handler {Handler}",
                handlerName
            );
        }
    }

    /// <summary>
    /// Wraps a Discord broadcast so a transport failure can't propagate out of an
    /// event handler.
    /// </summary>
    private async Task SafeBroadcastAsync(string message)
    {
        logger.LogInformation("{Message}", message);
        try
        {
            await discordClient.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to broadcast to Discord: {Message}", message);
        }
    }

    public async Task NotifyGameStartedAsync(string lobbyId)
    {
        LobbySession? session;
        lock (lockObject)
        {
            if (!activeSessions.TryGetValue(lobbyId, out session))
                session = null;
            else
            {
                session.State = LobbySessionState.InProgress;
                session.GameStartedAt = DateTime.UtcNow;
            }
        }

        if (session == null)
        {
            // Logger-only: idempotent admin calls (clicking Mark Game Started/Finished
            // twice, or the auto-call from OnLobbyReady when the session was never
            // created because no Steam client was available) shouldn't spam the channel.
            logger.LogDebug("NotifyGameStartedAsync: session {LobbyId} not found", lobbyId);
            return;
        }

        var message = LobbyMessages.GameStarted(lobbyId);
        logger.LogInformation("{Message}", message);
        await discordClient.SendMessageAsync(message);
        GameStarted?.Invoke(this, new GameStartedEventArgs { Session = session });
    }

    public async Task NotifyGameFinishedAsync(
        string lobbyId,
        string outcome = GameMetrics.OutcomeCompleted
    )
    {
        LobbySession? session;
        lock (lockObject)
        {
            if (!activeSessions.TryGetValue(lobbyId, out session))
                session = null;
            else
            {
                session.State = LobbySessionState.Finished;
                session.FinishedAt = DateTime.UtcNow;
                activeSessions.Remove(lobbyId);
            }
        }

        if (session == null)
        {
            // Logger-only — see comment above. !report also calls this and we
            // don't want every report to risk a noisy "Session not found" if the
            // lobby was finished some other way.
            logger.LogDebug("NotifyGameFinishedAsync: session {LobbyId} not found", lobbyId);
            return;
        }

        // Lobby-ready → finish wall-clock time, tagged by how the match ended.
        gameMetrics.RecordMatchFinished(
            outcome,
            (session.FinishedAt ?? DateTime.UtcNow) - session.StartedAt
        );

        // Each cleanup step is wrapped independently so a failure in step N
        // doesn't strand steps N+1..end. The previous form was sequential
        // awaits — a throw in ReleaseAccountAsync would leave the lobby
        // stuck in `Ready` state with no new lobby spawned, and players
        // couldn't queue again until restart.

        string? username = null;
        try
        {
            await accountPool.ReleaseAccountAsync(session.AccountId);
            username = accountPool.GetAccountById(session.AccountId)?.Username;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "NotifyGameFinishedAsync: failed to release account {AccountId}",
                session.AccountId
            );
        }

        await SafeBroadcastAsync(
            LobbyMessages.GameFinished(lobbyId, username ?? session.AccountId)
        );

        try
        {
            await lobbyQueue.FinishLobbyAsync(lobbyId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "NotifyGameFinishedAsync: failed to finish queue lobby {LobbyId}",
                lobbyId
            );
        }

        try
        {
            GameFinished?.Invoke(this, new GameFinishedEventArgs { Session = session });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NotifyGameFinishedAsync: GameFinished subscribers threw");
        }
    }

    /// <summary>
    /// Admin "destroy": force a hosted game to end now. Sends the Dota GC a
    /// PracticeLobbyLeave on the lobby's reserved bot account so the practice
    /// lobby is actually reaped on Steam's side, cancels the in-progress match
    /// (no rating change), then runs the normal finish/cleanup (releases the
    /// pool account, settles the queue lobby, fires GameFinished). Best-effort
    /// and idempotent: if the session or its Steam client can't be resolved we
    /// still run the finish cleanup so the account isn't stranded.
    /// </summary>
    public async Task DestroyLobbyAsync(string lobbyId)
    {
        var session = GetSession(lobbyId);
        if (session == null)
        {
            logger.LogWarning(
                "DestroyLobbyAsync: session {LobbyId} not found; running finish cleanup anyway",
                lobbyId
            );
        }
        else
        {
            var account = accountPool.GetAccountById(session.AccountId);
            var botClient = account == null ? null : steamGcPool.GetByUsername(account.Username);
            if (botClient == null)
            {
                logger.LogWarning(
                    "DestroyLobbyAsync: no Steam client for lobby {LobbyId} (account {AccountId}); finishing without GC leave",
                    lobbyId,
                    session.AccountId
                );
            }
            else
            {
                try
                {
                    await botClient.LeaveActiveLobbyAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "DestroyLobbyAsync: GC leave failed for lobby {LobbyId}",
                        lobbyId
                    );
                }
            }
        }

        // Settle the DB record too. The GC leave above clears the client's lobby
        // state before the GC's destroy notification arrives, so the usual
        // MatchCompleted path never fires for an admin destroy — without this the
        // match row would sit InProgress until the stale-match reconciliation on
        // the next startup. Null winner = Cancelled, no rating change; no-op if
        // the match was already settled some other way.
        try
        {
            var result = await matchRepository.CompleteMatchByLobbyAsync(
                lobbyId,
                winningTeam: null
            );
            logger.LogInformation("DestroyLobbyAsync: {Message}", result.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "DestroyLobbyAsync: failed to settle match for lobby {LobbyId}",
                lobbyId
            );
        }

        await NotifyGameFinishedAsync(lobbyId, GameMetrics.OutcomeCancelled);
    }

    /// <summary>
    /// Fired when the Dota GC reports a hosted match reaching POSTGAME. Persists
    /// the result (applying ELO when the game was scored), posts a result card to
    /// Discord, then runs the normal finish/cleanup so the account is released and
    /// players can queue again.
    /// </summary>
    private async Task HandleMatchCompletedAsync(string lobbyId, MatchCompletedEventArgs args)
    {
        ReportMatchResult? result = null;
        try
        {
            result = await matchRepository.CompleteMatchByLobbyAsync(lobbyId, args.WinningTeam);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "HandleMatchCompletedAsync: failed to persist result for lobby {LobbyId}",
                lobbyId
            );
        }

        if (result?.Match is { } match)
        {
            await SafeBroadcastAsync(LobbyMessages.MatchResult(match, args.MatchId));
        }
        else
        {
            // DB write failed or no match row — still surface what the GC told us.
            await SafeBroadcastAsync(
                LobbyMessages.MatchFinishedFallback(lobbyId, args.WinningTeam, args.MatchId)
            );
        }

        // Release the account and reopen the queue regardless of how the result
        // persisted — a stuck session is worse than a missing result card.
        await NotifyGameFinishedAsync(lobbyId);
    }

    public List<LobbySession> GetActiveSessions()
    {
        lock (lockObject)
        {
            return activeSessions.Values.ToList();
        }
    }

    /// <summary>
    /// Returns the active sessions as a Common DTO list so the API can serialise
    /// them without exposing the server-side Player/Lobby types.
    /// </summary>
    public List<ActiveSessionSummary> GetActiveSessionSummaries()
    {
        lock (lockObject)
        {
            return activeSessions
                .Values.Select(s => new ActiveSessionSummary
                {
                    LobbyId = s.LobbyId,
                    AccountId = s.AccountId,
                    PlayerCount = s.Players.Count,
                    State = (PoolSessionState)s.State,
                    StartedAt = s.StartedAt,
                    GameStartedAt = s.GameStartedAt,
                    FinishedAt = s.FinishedAt,
                })
                .ToList();
        }
    }

    public LobbySession? GetSession(string lobbyId)
    {
        lock (lockObject)
        {
            activeSessions.TryGetValue(lobbyId, out var session);
            return session;
        }
    }

    public SystemStatus GetSystemStatus() =>
        new()
        {
            PoolStats = GetAccountPoolStats(),
            QueueStats = GetQueueStats(),
            ActiveSessions = GetActiveSessions(),
            Timestamp = DateTime.UtcNow,
        };
}
