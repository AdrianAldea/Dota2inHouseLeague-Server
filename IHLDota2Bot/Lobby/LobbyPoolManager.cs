using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Data.Repositories;
using IHLDota2Bot.Services;
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
    private readonly SteamGcClientPool steamGcPool;
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
        SteamGcClientPool steamGcPool,
        IOptions<SteamOptions> steamOptions,
        ILogger<LobbyPoolManager> logger)
    {
        ArgumentNullException.ThrowIfNull(accountPool);
        ArgumentNullException.ThrowIfNull(lobbyQueue);
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(matchRepository);
        ArgumentNullException.ThrowIfNull(steamGcPool);
        ArgumentNullException.ThrowIfNull(steamOptions);
        ArgumentNullException.ThrowIfNull(logger);

        this.accountPool = accountPool;
        this.lobbyQueue = lobbyQueue;
        this.discordClient = discordClient;
        this.matchRepository = matchRepository;
        this.steamGcPool = steamGcPool;
        this.steamOptions = steamOptions.Value;
        this.logger = logger;

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
            var stale = await matchRepository.GetStaleInProgressMatchesAsync(olderThan, cancellationToken);
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
                        match.Id, match.CreatedAt, (DateTime.UtcNow - match.CreatedAt).TotalHours);
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

    public Task<QueueOperationResponse> JoinQueueAsync(ulong userId, string username, string steamId) =>
        lobbyQueue.JoinQueueAsync(userId, username, steamId);

    public Task<QueueOperationResponse> LeaveQueueAsync(ulong userId) =>
        lobbyQueue.LeaveQueueAsync(userId);

    public Task<QueueOperationResponse> MarkPlayerReadyAsync(ulong userId) =>
        lobbyQueue.MarkPlayerReadyAsync(userId);

    public Task<QueueOperationResponse> MarkPlayerUnreadyAsync(ulong userId) =>
        lobbyQueue.MarkPlayerUnreadyAsync(userId);

    public QueueStatusResponse GetQueueStatus() => lobbyQueue.GetQueueStatus();

    public bool RemovePlayerCompletely(ulong userId) => lobbyQueue.RemovePlayerCompletely(userId);

    public bool UpdatePlayerSteamId(ulong userId, string steamId) => lobbyQueue.UpdatePlayerSteamId(userId, steamId);

    public Task<QueueOperationResponse> KickFromLobbyAsync(ulong userId) => lobbyQueue.KickFromLobbyAsync(userId);

    public Task<QueueOperationResponse> ForceLobbyReadyAsync() => lobbyQueue.ForceLobbyReadyAsync();

    public Task<QueueOperationResponse> ResetCurrentLobbyAsync() => lobbyQueue.ResetCurrentLobbyAsync();

    public async Task<QueueOperationResponse> AddSteamAccountAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return new QueueOperationResponse { Success = false, Message = "Username and password are required." };

        if (accountPool.GetAccountByUsername(username) != null)
            return new QueueOperationResponse { Success = false, Message = $"Account {username} already exists." };

        // Register the Steam connection FIRST. If we don't have a real Steam
        // client for this account, reserving it from the bookkeeping pool later
        // would silently fail to create a Dota lobby. Better to refuse the add
        // outright than leave a half-wired account behind.
        var (gcAdded, gcMessage) = await steamGcPool.TryAddAccountAsync(username, password);
        if (!gcAdded)
        {
            logger.LogWarning("Refusing AddSteamAccount({Username}): SteamGcClientPool refused — {Message}", username, gcMessage);
            return new QueueOperationResponse { Success = false, Message = gcMessage };
        }

        await accountPool.RegisterAccountAsync(username, password);
        return new QueueOperationResponse
        {
            Success = true,
            Message = $"Steam account {username} registered and connecting."
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
            Accounts = stats.Accounts
                .Select(a => new PoolAccountSummary
                {
                    Username = a.Username,
                    State = (PoolAccountState)a.State,
                    CurrentLobbyId = a.CurrentLobbyId,
                    LastUsedAt = a.LastUsedAt
                })
                .ToList()
        };
    }

    public Lobby GetCurrentLobby() => lobbyQueue.GetCurrentLobby();
    public QueueStats GetQueueStats() => lobbyQueue.GetStats();
    public List<Player> GetRegisteredPlayers() => lobbyQueue.GetRegisteredPlayers();

    private async void OnLobbyReady(object? sender, LobbyReadyEventArgs e)
    {
        try
        {
            var lobby = e.Lobby;
            var account = await accountPool.GetAvailableAccountAsync();

            if (account == null)
            {
                var message = $"No available Steam accounts for lobby {lobby.Id}. Please wait...";
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
                    StartedAt = DateTime.UtcNow
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
                var shuffled = lobby.Players.OrderBy(_ => Guid.NewGuid()).ToList();
                var half = shuffled.Count / 2;
                var teamAssignments = shuffled
                    .Select((p, idx) => (p.UserId, Team: idx < half ? MatchTeam.Radiant : MatchTeam.Dire))
                    .ToList();

                var match = await matchRepository.CreateMatchAsync(lobby.Id, teamAssignments);
                logger.LogInformation("Match {MatchId} created for lobby {LobbyId}", match.Id, lobby.Id);
                matchPersisted = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist match for lobby {LobbyId}; rolling back reservation", lobby.Id);
            }

            if (!matchPersisted)
            {
                lock (lockObject)
                {
                    activeSessions.Remove(lobby.Id);
                    lobby.SteamAccountId = null;
                }
                await accountPool.ReleaseAccountAsync(account.Id);
                await SafeBroadcastAsync(
                    $"⚠️ Lobby {lobby.Id} could not be persisted to the database. Account {account.Username} released; please retry.");
                return;
            }

            // Tell Steam to spin up the actual Dota 2 practice lobby and invite the 10 players.
            // The Steam GC operations run on the *specific* SteamGcClient for the reserved
            // pool account, so multiple concurrent lobbies can co-exist on distinct bots.
            var lobbyShortId = lobby.Id.Length >= 8 ? lobby.Id[..8] : lobby.Id;
            var lobbyName = $"Dota2IHL-{lobbyShortId}";
            var lobbyPassword = $"Dota2IHL-{lobbyShortId}";

            var botClient = steamGcPool.GetByUsername(account.Username);
            if (botClient == null)
            {
                logger.LogError(
                    "Lobby {LobbyId} reserved pool account {Username}, but no SteamGcClient is registered for that name. " +
                    "Add the account via Steam:PoolAccounts in configuration, or use !add_account at runtime.",
                    lobby.Id, account.Username);
            }
            else if (!botClient.IsLoggedOn)
            {
                logger.LogWarning(
                    "Lobby {LobbyId} reserved pool account {Username}, but its Steam client isn't logged on yet. " +
                    "Skipping CreateLobby / invites. Match still persisted; admin can invoke /admin/game/finished/{LobbyId} to release the account.",
                    lobby.Id, account.Username, lobby.Id);
            }
            else
            {
                try
                {
                    var steamIds = lobby.Players
                        .Select(p => ulong.TryParse(p.SteamId, out var id) ? id : 0UL)
                        .Where(id => id > 0)
                        .ToList();

                    if (steamIds.Count == 0)
                    {
                        logger.LogWarning("Lobby {LobbyId} ready but no parsable Steam IDs among its players; auto-launch disabled", lobby.Id);
                    }

                    // Subscribe before CreateLobbyAsync — the GC snapshot can land
                    // immediately. Handler self-detaches once it fires for this lobby.
                    var lobbyId = lobby.Id;
                    EventHandler<LobbyLaunchedEventArgs>? handler = null;
                    handler = async (_, args) =>
                    {
                        if (args.AppLobbyId != lobbyId) return;
                        botClient.LobbyLaunched -= handler;
                        logger.LogInformation(
                            "Lobby {LobbyId} launched in Dota (DotaLobbyId={DotaLobbyId}); flipping to InProgress",
                            lobbyId, args.DotaLobbyId);
                        try
                        {
                            await NotifyGameStartedAsync(lobbyId);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to flip game-started state for lobby {LobbyId}", lobbyId);
                        }
                    };
                    botClient.LobbyLaunched += handler;

                    // The GC tells us the result when the match reaches POSTGAME.
                    // Subscribe a self-detaching handler so the win is recorded and
                    // posted to Discord without anyone having to !report it.
                    EventHandler<MatchCompletedEventArgs>? completedHandler = null;
                    completedHandler = async (_, args) =>
                    {
                        if (args.AppLobbyId != lobbyId) return;
                        botClient.MatchCompleted -= completedHandler;
                        try
                        {
                            await HandleMatchCompletedAsync(lobbyId, args);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to handle match completion for lobby {LobbyId}", lobbyId);
                        }
                    };
                    botClient.MatchCompleted += completedHandler;

                    await botClient.CreateLobbyAsync(new CreateLobbyRequest
                    {
                        AuthorId = account.Id,
                        LobbyName = lobbyName,
                        LobbyPassword = lobbyPassword,
                        ExpectedSteamIds = steamIds,
                        AppLobbyId = lobby.Id
                    });

                    if (steamIds.Count > 0)
                    {
                        botClient.InvitePlayersToLobby(steamIds);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create Dota lobby / send invites for {LobbyId}", lobby.Id);
                }
            }

            var readyMessage = $"Lobby {lobby.Id} is ready! Steam lobby `{lobbyName}` (password `{lobbyPassword}`). Assigned account: {account.Username}";
            logger.LogInformation("{Message}", readyMessage);
            await discordClient.SendMessageAsync(readyMessage);

            GameReady?.Invoke(this, new GameReadyEventArgs { Lobby = lobby, Account = account, Session = session });

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

    private async void OnLobbyCreated(object? sender, LobbyCreatedEventArgs e)
    {
        var message = $"New lobby created: {e.Lobby.Id} ({e.Lobby.Players.Count} players)";
        await SafeBroadcastAsync(message);
    }

    private async void OnPlayerJoined(object? sender, PlayerJoinedEventArgs e)
    {
        var message = $"{e.Player.Username} joined the queue ({e.Lobby.Players.Count}/{LeagueConstants.PlayersPerLobby})";
        await SafeBroadcastAsync(message);
    }

    private async void OnPlayerLeft(object? sender, PlayerLeftEventArgs e)
    {
        var message = $"{e.Player.Username} left the queue ({e.Lobby.Players.Count}/{LeagueConstants.PlayersPerLobby})";
        await SafeBroadcastAsync(message);
    }

    private async void OnPlayerReady(object? sender, PlayerReadyEventArgs e)
    {
        var lobby = lobbyQueue.GetCurrentLobby();
        var message = $"{e.Player.Username} is ready! ({lobby.ReadyCount}/{lobby.TotalCount})";
        await SafeBroadcastAsync(message);
    }

    private async void OnPlayerUnready(object? sender, PlayerUnreadyEventArgs e)
    {
        var message = $"{e.Player.Username} is no longer ready";
        await SafeBroadcastAsync(message);
    }

    private async void OnPlayerKicked(object? sender, PlayerKickedEventArgs e)
    {
        var message = $"{e.Player.Username} was kicked from the lobby ({e.Lobby.Players.Count}/{LeagueConstants.PlayersPerLobby})";
        await SafeBroadcastAsync(message);
    }

    private async void OnLobbyReset(object? sender, LobbyResetEventArgs e)
    {
        var message = $"Queue reset. {e.RemovedPlayerCount} player(s) removed from the current lobby.";
        await SafeBroadcastAsync(message);
    }

    private async void OnLobbyFinished(object? sender, LobbyFinishedEventArgs e)
    {
        var message = $"Lobby {e.Lobby.Id} has finished. Players can now join the next lobby.";
        await SafeBroadcastAsync(message);
    }

    private async void OnLobbyFull(object? sender, LobbyFullEventArgs e)
    {
        var message = $"Lobby {e.Lobby.Id} is full ({e.Lobby.Players.Count}/{LeagueConstants.PlayersPerLobby}). Waiting for everyone to !ready.";
        await SafeBroadcastAsync(message);
    }

    /// <summary>
    /// Wraps a Discord broadcast so a transport failure can't kill the event-handler
    /// thread (these are async void handlers).
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

        var message = $"GAME STARTED: {lobbyId}";
        logger.LogInformation("{Message}", message);
        await discordClient.SendMessageAsync(message);
        GameStarted?.Invoke(this, new GameStartedEventArgs { Session = session });
    }

    public async Task NotifyGameFinishedAsync(string lobbyId)
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
            logger.LogError(ex, "NotifyGameFinishedAsync: failed to release account {AccountId}", session.AccountId);
        }

        await SafeBroadcastAsync($"GAME FINISHED: {lobbyId} | Account {username ?? session.AccountId} released");

        try
        {
            await lobbyQueue.FinishLobbyAsync(lobbyId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "NotifyGameFinishedAsync: failed to finish queue lobby {LobbyId}", lobbyId);
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
            logger.LogError(ex, "HandleMatchCompletedAsync: failed to persist result for lobby {LobbyId}", lobbyId);
        }

        if (result?.Match is { } match)
        {
            await SafeBroadcastAsync(FormatMatchResult(match, args));
        }
        else
        {
            // DB write failed or no match row — still surface what the GC told us.
            var winnerText = args.WinningTeam?.ToString() ?? "No winner (not scored)";
            await SafeBroadcastAsync(
                $"🏁 Match for lobby {lobbyId} finished. Winner: {winnerText} (Dota match {args.MatchId}).");
        }

        // Release the account and reopen the queue regardless of how the result
        // persisted — a stuck session is worse than a missing result card.
        await NotifyGameFinishedAsync(lobbyId);
    }

    private static string FormatMatchResult(MatchSummary match, MatchCompletedEventArgs args)
    {
        var header = match.WinningTeam switch
        {
            MatchTeam.Radiant => "🏆 **Radiant Victory!**",
            MatchTeam.Dire => "🏆 **Dire Victory!**",
            _ => "🏁 **Match finished** (not scored)"
        };

        var radiant = match.Players.Where(p => p.Team == MatchTeam.Radiant).Select(p => p.Username);
        var dire = match.Players.Where(p => p.Team == MatchTeam.Dire).Select(p => p.Username);

        var lines = new List<string>
        {
            header,
            $"Dota match `{args.MatchId}`",
            $"**Radiant:** {string.Join(", ", radiant)}",
            $"**Dire:** {string.Join(", ", dire)}"
        };

        return string.Join("\n", lines);
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
            return activeSessions.Values
                .Select(s => new ActiveSessionSummary
                {
                    LobbyId = s.LobbyId,
                    AccountId = s.AccountId,
                    PlayerCount = s.Players.Count,
                    State = (PoolSessionState)s.State,
                    StartedAt = s.StartedAt,
                    GameStartedAt = s.GameStartedAt,
                    FinishedAt = s.FinishedAt
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

    public SystemStatus GetSystemStatus() => new()
    {
        PoolStats = GetAccountPoolStats(),
        QueueStats = GetQueueStats(),
        ActiveSessions = GetActiveSessions(),
        Timestamp = DateTime.UtcNow
    };
}
