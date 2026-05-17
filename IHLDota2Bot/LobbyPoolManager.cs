using IHLDota2Bot.Clients;

namespace IHLDota2Bot
{
    /// <summary>
    /// Main orchestrator for the pool system
    /// Manages Steam account pool and lobby queue
    /// </summary>
    public class LobbyPoolManager
    {
        public SteamAccountPool _accountPool;
        private readonly DiscordClient _discordClient;
        private readonly LobbyQueue _lobbyQueue;
        public Dictionary<string, LobbySession> _activeSessions = new();
        private readonly object _lockObject = new object();

        // Events
        public event EventHandler<GameReadyEventArgs> GameReady;
        public event EventHandler<GameStartedEventArgs> GameStarted;
        public event EventHandler<GameFinishedEventArgs> GameFinished;

        public LobbyPoolManager(DiscordClient discordClient, LobbyQueue lobbyQueue)
        {
            _accountPool = new SteamAccountPool();
            _discordClient = discordClient;
            _lobbyQueue = lobbyQueue;

            RegisterSteamAccount("dota2inhouseleague", "Romania10.");
            RegisterSteamAccount("dota2inhouseleague2", "Romania10.");
            RegisterSteamAccount("dota2inhouseleague3", "Romania10.");

            _lobbyQueue.LobbyReady += OnLobbyReady;
            _lobbyQueue.LobbyCreated += OnLobbyCreated;
            _lobbyQueue.PlayerReady += OnPlayerReady;
        }

        // ==================== Account Pool Management ====================

        /// <summary>
        /// Register a Steam account
        /// </summary>
        public void RegisterSteamAccount(string username, string password)
        {
            _accountPool.RegisterAccount(username, password);
        }

        /// <summary>
        /// Get account pool statistics
        /// </summary>
        public AccountPoolStats GetAccountPoolStats()
        {
            return _accountPool.GetStats();
        }

        /// <summary>
        /// Get all Steam accounts
        /// </summary>
        public List<SteamAccount> GetSteamAccounts()
        {
            return _accountPool.GetAllAccounts();
        }

        /// <summary>
        /// Get number of available accounts
        /// </summary>
        public int GetAvailableAccountCount()
        {
            return _accountPool.GetAvailableAccountCount();
        }

        // ==================== Player & Lobby Management ====================

        /// <summary>
        /// Register a player to the system
        /// </summary>
        public void RegisterPlayer(ulong userId, string username, string steamId)
        {
            _lobbyQueue.RegisterPlayer(userId, username, steamId);
        }

        /// <summary>
        /// Mark a player as ready
        /// </summary>
        public void MarkPlayerReady(ulong userId)
        {
            _lobbyQueue.MarkPlayerReady(userId);
        }

        /// <summary>
        /// Get the current active lobby
        /// </summary>
        public Lobby GetCurrentLobby()
        {
            return _lobbyQueue.GetCurrentLobby();
        }

        /// <summary>
        /// Get queue statistics
        /// </summary>
        public QueueStats GetQueueStats()
        {
            return _lobbyQueue.GetStats();
        }

        /// <summary>
        /// Get all registered players
        /// </summary>
        public List<Player> GetRegisteredPlayers()
        {
            return _lobbyQueue.GetRegisteredPlayers();
        }

        // ==================== Game Session Management ====================

        /// <summary>
        /// Notify when a game is ready to start
        /// </summary>
        public void OnLobbyReady(object sender, LobbyReadyEventArgs e)
        {
            lock (_lockObject)
            {
                var lobby = e.Lobby;
                var account = _accountPool.GetAvailableAccount();

                if (account == null)
                {
                    var message = $"❌ No available Steam accounts for lobby {lobby.Id}. Please wait...";
                    _discordClient.SendMessageAsync(message);
                    Console.WriteLine(message);
                    return;
                }

                // Create session
                var session = new LobbySession
                {
                    LobbyId = lobby.Id,
                    AccountId = account.Id,
                    Players = lobby.Players.ToList(),
                    State = LobbySessionState.Ready,
                    StartedAt = DateTime.UtcNow
                };

                _activeSessions[lobby.Id] = session;
                lobby.SteamAccountId = account.Id;
                _accountPool.MarkAccountInUse(account.Id, lobby.Id);

                var readyMessage = $"✅ Lobby {lobby.Id} is ready! Assigned account: {account.Username}";
                _discordClient.SendMessageAsync(readyMessage);
                Console.WriteLine(readyMessage);
                
                GameReady?.Invoke(this, new GameReadyEventArgs { Lobby = lobby, Account = account, Session = session });
            }
        }

        /// <summary>
        /// Notify when lobby is created
        /// </summary>
        public void OnLobbyCreated(object sender, LobbyCreatedEventArgs e)
        {
            var message = $"📋 New lobby created: {e.Lobby.Id} ({e.Lobby.Players.Count} players)";
            _discordClient.SendMessageAsync(message);
            Console.WriteLine(message);
        }

        /// <summary>
        /// Notify when player is ready
        /// </summary>
        public void OnPlayerReady(object sender, PlayerReadyEventArgs e)
        {
            var lobby = _lobbyQueue.GetCurrentLobby();
            var message = $"✅ {e.Player.Username} is ready! ({lobby.ReadyCount}/{lobby.TotalCount})";
            _discordClient.SendMessageAsync(message);
            Console.WriteLine(message);
        }

        /// <summary>
        /// Notify when game has started
        /// </summary>
        public void NotifyGameStarted(string lobbyId)
        {
            lock (_lockObject)
            {
                if (!_activeSessions.TryGetValue(lobbyId, out var session))
                {
                    var message = $"⚠️ Session {lobbyId} not found";
                    _discordClient.SendMessageAsync(message);
                    Console.WriteLine(message);
                    return;
                }

                session.State = LobbySessionState.InProgress;
                session.GameStartedAt = DateTime.UtcNow;

                var gameStartedMessage = $"🎮 GAME STARTED: {lobbyId}";
                _discordClient.SendMessageAsync(gameStartedMessage);
                Console.WriteLine(gameStartedMessage);
                GameStarted?.Invoke(this, new GameStartedEventArgs { Session = session });
            }
        }

        /// <summary>
        /// Notify when game has finished
        /// </summary>
        public void NotifyGameFinished(string lobbyId)
        {
            lock (_lockObject)
            {
                if (!_activeSessions.TryGetValue(lobbyId, out var session))
                {
                    var message = $"⚠️ Session {lobbyId} not found";
                    _discordClient.SendMessageAsync(message);
                    Console.WriteLine(message);
                    return;
                }

                session.State = LobbySessionState.Finished;
                session.FinishedAt = DateTime.UtcNow;

                // Release the account back to pool
                _accountPool.ReleaseAccount(session.AccountId);
                var account = _accountPool.GetAccountById(session.AccountId);

                var gameFinishedMessage = $"✅ GAME FINISHED: {lobbyId} | Account {account?.Username} released";
                _discordClient.SendMessageAsync(gameFinishedMessage);
                Console.WriteLine(gameFinishedMessage);

                // Finish the lobby
                _lobbyQueue.FinishLobby(lobbyId);

                // Remove from active sessions
                _activeSessions.Remove(lobbyId);

                GameFinished?.Invoke(this, new GameFinishedEventArgs { Session = session });
            }
        }

        /// <summary>
        /// Get all active game sessions
        /// </summary>
        public List<LobbySession> GetActiveSessions()
        {
            lock (_lockObject)
            {
                return _activeSessions.Values.ToList();
            }
        }

        /// <summary>
        /// Get a specific session
        /// </summary>
        public LobbySession GetSession(string lobbyId)
        {
            lock (_lockObject)
            {
                _activeSessions.TryGetValue(lobbyId, out var session);
                return session;
            }
        }

        // ==================== System Status ====================

        /// <summary>
        /// Get complete system status
        /// </summary>
        public SystemStatus GetSystemStatus()
        {
            return new SystemStatus
            {
                PoolStats = GetAccountPoolStats(),
                QueueStats = GetQueueStats(),
                ActiveSessions = GetActiveSessions(),
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Print system status to console
        /// </summary>
        public void PrintSystemStatus()
        {
            var status = GetSystemStatus();

            string output = $@"
                ╔════════════════════════════════════════════════════════════╗
                ║          POOL SYSTEM STATUS                                ║
                ╚════════════════════════════════════════════════════════════╝

                📊 STEAM ACCOUNT POOL:
                   Total: {status.PoolStats.TotalAccounts}
                   Ready: {status.PoolStats.ReadyAccounts} ✅
                   In Use: {status.PoolStats.InUseAccounts} 🎮
                   Cooldown: {status.PoolStats.OnCooldownAccounts} ⏳

                👥 QUEUE STATUS:
                   Registered Players: {status.QueueStats.TotalRegisteredPlayers}
                   Waiting: {status.QueueStats.WaitingPlayers}
                   Current Lobby: {status.QueueStats.CurrentLobbyPlayerCount}/10
                   Ready: {status.QueueStats.PlayersReady}/{status.QueueStats.CurrentLobbyPlayerCount}

                🎮 ACTIVE GAMES:
                ";
            _discordClient.SendMessageAsync(output);
            Console.WriteLine(output);

            if (status.ActiveSessions.Count == 0)
            {
                var noGamesMessage = "   No active games";
                _discordClient.SendMessageAsync(noGamesMessage);
                Console.WriteLine(noGamesMessage);
            }
            else
            {
                foreach (var session in status.ActiveSessions)
                {
                    var duration = DateTime.UtcNow - session.StartedAt;
                    var gameFinishedMessage = $"   Lobby {session.LobbyId}: {session.State} - {PoolExtensions.FormatDuration(duration)}";
                    _discordClient.SendMessageAsync(gameFinishedMessage);
                    Console.WriteLine(gameFinishedMessage);
                }
            }

            var timestampMessage = $"   Updated: {status.Timestamp:dd/MM/yyyy HH:mm:ss}";
            _discordClient.SendMessageAsync(timestampMessage);
            Console.WriteLine(timestampMessage);
        }
    }

    // ==================== Data Structures ====================

    /// <summary>
    /// Represents a game session
    /// </summary>
    public class LobbySession
    {
        public string LobbyId { get; set; }
        public string AccountId { get; set; }
        public List<Player> Players { get; set; }
        public LobbySessionState State { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? GameStartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }

        public override string ToString()
        {
            return $"Session: {LobbyId} | Account: {AccountId} | State: {State} | Players: {Players.Count}";
        }
    }

    /// <summary>
    /// Game session state
    /// </summary>
    public enum LobbySessionState
    {
        Ready,       // Ready to start
        InProgress,  // Game is running
        Finished     // Game finished
    }

    /// <summary>
    /// Complete system status
    /// </summary>
    public class SystemStatus
    {
        public AccountPoolStats PoolStats { get; set; }
        public QueueStats QueueStats { get; set; }
        public List<LobbySession> ActiveSessions { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // ==================== Event Args ====================

    public class GameReadyEventArgs : EventArgs
    {
        public Lobby Lobby { get; set; }
        public SteamAccount Account { get; set; }
        public LobbySession Session { get; set; }
    }

    public class GameStartedEventArgs : EventArgs
    {
        public LobbySession Session { get; set; }
    }

    public class GameFinishedEventArgs : EventArgs
    {
        public LobbySession Session { get; set; }
    }
}
