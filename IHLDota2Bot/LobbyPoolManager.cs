using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Data.Repositories;
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
        IOptions<SteamOptions> steamOptions,
        ILogger<LobbyPoolManager> logger)
    {
        ArgumentNullException.ThrowIfNull(accountPool);
        ArgumentNullException.ThrowIfNull(lobbyQueue);
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(matchRepository);
        ArgumentNullException.ThrowIfNull(steamOptions);
        ArgumentNullException.ThrowIfNull(logger);

        this.accountPool = accountPool;
        this.lobbyQueue = lobbyQueue;
        this.discordClient = discordClient;
        this.matchRepository = matchRepository;
        this.steamOptions = steamOptions.Value;
        this.logger = logger;

        this.lobbyQueue.LobbyReady += OnLobbyReady;
        this.lobbyQueue.LobbyCreated += OnLobbyCreated;
        this.lobbyQueue.PlayerReady += OnPlayerReady;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var account in steamOptions.PoolAccounts)
        {
            await accountPool.RegisterAccountAsync(account.Username, account.Password);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public AccountPoolStats GetAccountPoolStats() => accountPool.GetStats();
    public List<SteamAccount> GetSteamAccounts() => accountPool.GetAllAccounts();
    public int GetAvailableAccountCount() => accountPool.GetAvailableAccountCount();

    public Task RegisterPlayerAsync(ulong userId, string username, string steamId) =>
        lobbyQueue.RegisterPlayerAsync(userId, username, steamId);

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

        await accountPool.RegisterAccountAsync(username, password);
        return new QueueOperationResponse { Success = true, Message = $"Steam account {username} registered." };
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

            try
            {
                var shuffled = lobby.Players.OrderBy(_ => Guid.NewGuid()).ToList();
                var half = shuffled.Count / 2;
                var teamAssignments = shuffled
                    .Select((p, idx) => (p.UserId, Team: idx < half ? MatchTeam.Radiant : MatchTeam.Dire))
                    .ToList();

                var match = await matchRepository.CreateMatchAsync(lobby.Id, teamAssignments);
                logger.LogInformation("Match {MatchId} created for lobby {LobbyId}", match.Id, lobby.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist match for lobby {LobbyId}", lobby.Id);
            }

            var readyMessage = $"Lobby {lobby.Id} is ready! Assigned account: {account.Username}";
            logger.LogInformation("{Message}", readyMessage);
            await discordClient.SendMessageAsync(readyMessage);

            GameReady?.Invoke(this, new GameReadyEventArgs { Lobby = lobby, Account = account, Session = session });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling LobbyReady event");
        }
    }

    private async void OnLobbyCreated(object? sender, LobbyCreatedEventArgs e)
    {
        var message = $"New lobby created: {e.Lobby.Id} ({e.Lobby.Players.Count} players)";
        logger.LogInformation("{Message}", message);
        await discordClient.SendMessageAsync(message);
    }

    private async void OnPlayerReady(object? sender, PlayerReadyEventArgs e)
    {
        var lobby = lobbyQueue.GetCurrentLobby();
        var message = $"{e.Player.Username} is ready! ({lobby.ReadyCount}/{lobby.TotalCount})";
        logger.LogInformation("{Message}", message);
        await discordClient.SendMessageAsync(message);
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
            var warn = $"Session {lobbyId} not found";
            logger.LogWarning("{Message}", warn);
            await discordClient.SendMessageAsync(warn);
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
            var warn = $"Session {lobbyId} not found";
            logger.LogWarning("{Message}", warn);
            await discordClient.SendMessageAsync(warn);
            return;
        }

        await accountPool.ReleaseAccountAsync(session.AccountId);
        var account = accountPool.GetAccountById(session.AccountId);

        var message = $"GAME FINISHED: {lobbyId} | Account {account?.Username} released";
        logger.LogInformation("{Message}", message);
        await discordClient.SendMessageAsync(message);

        await lobbyQueue.FinishLobbyAsync(lobbyId);

        GameFinished?.Invoke(this, new GameFinishedEventArgs { Session = session });
    }

    public List<LobbySession> GetActiveSessions()
    {
        lock (lockObject)
        {
            return activeSessions.Values.ToList();
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

public class LobbySession
{
    public string LobbyId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public List<Player> Players { get; set; } = [];
    public LobbySessionState State { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? GameStartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public override string ToString() =>
        $"Session: {LobbyId} | Account: {AccountId} | State: {State} | Players: {Players.Count}";
}

public enum LobbySessionState
{
    Ready,
    InProgress,
    Finished
}

public class SystemStatus
{
    public AccountPoolStats PoolStats { get; set; } = new();
    public QueueStats QueueStats { get; set; } = new();
    public List<LobbySession> ActiveSessions { get; set; } = [];
    public DateTime Timestamp { get; set; }
}

public class GameReadyEventArgs : EventArgs
{
    public required Lobby Lobby { get; set; }
    public required SteamAccount Account { get; set; }
    public required LobbySession Session { get; set; }
}

public class GameStartedEventArgs : EventArgs
{
    public required LobbySession Session { get; set; }
}

public class GameFinishedEventArgs : EventArgs
{
    public required LobbySession Session { get; set; }
}
