using System;
using System.Collections.Generic;
using System.Linq;

namespace IHLDota2Bot
{
    /// <summary>
    /// Manages the lobby queue and player registration
    /// Handles automatic lobby creation when enough players are ready
    /// </summary>
    public class LobbyQueue
    {
        public const int PLAYERS_PER_LOBBY = 10;

        private List<Player> _registeredPlayers = new();
        private List<Lobby> _lobbies = new();
        private Lobby _currentLobby;
        private readonly object _lockObject = new object();

        // Events
        public event EventHandler<LobbyCreatedEventArgs> LobbyCreated;
        public event EventHandler<PlayerJoinedEventArgs> PlayerJoined;
        public event EventHandler<PlayerReadyEventArgs> PlayerReady;
        public event EventHandler<LobbyFullEventArgs> LobbyFull;
        public event EventHandler<LobbyReadyEventArgs> LobbyReady;

        public LobbyQueue()
        {
            CreateNewLobby();
        }

        /// <summary>
        /// Register a player to the system
        /// </summary>
        public void RegisterPlayer(ulong userId, string username, string steamId)
        {
            lock (_lockObject)
            {
                if (_registeredPlayers.Any(p => p.UserId == userId))
                {
                    Console.WriteLine($"Player {username} already registered");
                    return;
                }

                var player = new Player
                {
                    UserId = userId,
                    Username = username,
                    SteamId = steamId,
                    RegisteredAt = DateTime.UtcNow
                };

                _registeredPlayers.Add(player);
                
                // Auto-add to current lobby if it's not full
                if (_currentLobby.Players.Count < PLAYERS_PER_LOBBY)
                {
                    _currentLobby.Players.Add(player);
                    player.CurrentLobbyId = _currentLobby.Id;
                    PlayerJoined?.Invoke(this, new PlayerJoinedEventArgs { Player = player, Lobby = _currentLobby });
                    
                    // Check if lobby is full
                    if (_currentLobby.Players.Count == PLAYERS_PER_LOBBY)
                    {
                        _currentLobby.State = LobbyState.Waiting;
                        LobbyFull?.Invoke(this, new LobbyFullEventArgs { Lobby = _currentLobby });
                    }
                }

                Console.WriteLine($"Player {username} registered. Queue: {_currentLobby.Players.Count}/{PLAYERS_PER_LOBBY}");
            }
        }

        /// <summary>
        /// Mark a player as ready in their current lobby
        /// </summary>
        public void MarkPlayerReady(ulong userId)
        {
            lock (_lockObject)
            {
                var player = _registeredPlayers.FirstOrDefault(p => p.UserId == userId);
                if (player == null || player.CurrentLobbyId == null)
                {
                    Console.WriteLine($"Player {userId} not in a lobby");
                    return;
                }

                player.IsReady = true;
                PlayerReady?.Invoke(this, new PlayerReadyEventArgs { Player = player });

                // Check if all players in lobby are ready
                if (CheckLobbyAllReady(player.CurrentLobbyId))
                {
                    var lobby = _lobbies.FirstOrDefault(l => l.Id == player.CurrentLobbyId);
                    if (lobby != null)
                    {
                        lobby.State = LobbyState.Ready;
                        LobbyReady?.Invoke(this, new LobbyReadyEventArgs { Lobby = lobby });
                        Console.WriteLine($"Lobby {lobby.Id} is READY! All players ready.");
                    }
                }
            }
        }

        /// <summary>
        /// Check if all players in a lobby are ready
        /// </summary>
        public bool CheckLobbyAllReady(string lobbyId)
        {
            var lobby = _lobbies.FirstOrDefault(l => l.Id == lobbyId);
            if (lobby == null) return false;
            return lobby.Players.Count == PLAYERS_PER_LOBBY && lobby.Players.All(p => p.IsReady);
        }

        /// <summary>
        /// Mark a lobby as finished and create a new one
        /// </summary>
        public void FinishLobby(string lobbyId)
        {
            lock (_lockObject)
            {
                var lobby = _lobbies.FirstOrDefault(l => l.Id == lobbyId);
                if (lobby != null)
                {
                    lobby.State = LobbyState.Finished;
                    lobby.FinishedAt = DateTime.UtcNow;
                    
                    // Remove players from this lobby
                    foreach (var player in lobby.Players)
                    {
                        player.CurrentLobbyId = null;
                        player.IsReady = false;
                    }

                    Console.WriteLine($"Lobby {lobbyId} finished");

                    // Create new lobby for waiting players
                    CreateNewLobby();
                }
            }
        }

        /// <summary>
        /// Create a new lobby and auto-fill with waiting players
        /// </summary>
        private void CreateNewLobby()
        {
            lock (_lockObject)
            {
                _currentLobby = new Lobby
                {
                    Id = Guid.NewGuid().ToString(),
                    Players = new List<Player>(),
                    State = LobbyState.Waiting,
                    CreatedAt = DateTime.UtcNow
                };

                _lobbies.Add(_currentLobby);

                // Auto-fill with available registered players not in any lobby
                var availablePlayers = _registeredPlayers
                    .Where(p => p.CurrentLobbyId == null)
                    .Take(PLAYERS_PER_LOBBY)
                    .ToList();

                foreach (var player in availablePlayers)
                {
                    _currentLobby.Players.Add(player);
                    player.CurrentLobbyId = _currentLobby.Id;
                    player.IsReady = false;
                }

                LobbyCreated?.Invoke(this, new LobbyCreatedEventArgs { Lobby = _currentLobby });
                Console.WriteLine($"New lobby created: {_currentLobby.Id} with {_currentLobby.Players.Count} players");

                // If we got exactly the required players, the lobby is full
                if (_currentLobby.Players.Count == PLAYERS_PER_LOBBY)
                {
                    _currentLobby.State = LobbyState.Waiting;
                    LobbyFull?.Invoke(this, new LobbyFullEventArgs { Lobby = _currentLobby });
                }
            }
        }

        /// <summary>
        /// Get the current active lobby
        /// </summary>
        public Lobby GetCurrentLobby()
        {
            lock (_lockObject)
            {
                return _currentLobby;
            }
        }

        /// <summary>
        /// Get a specific lobby by ID
        /// </summary>
        public Lobby GetLobbyById(string lobbyId)
        {
            lock (_lockObject)
            {
                return _lobbies.FirstOrDefault(l => l.Id == lobbyId);
            }
        }

        /// <summary>
        /// Get queue statistics
        /// </summary>
        public QueueStats GetStats()
        {
            lock (_lockObject)
            {
                return new QueueStats
                {
                    TotalRegisteredPlayers = _registeredPlayers.Count,
                    CurrentLobby = _currentLobby,
                    CurrentLobbyPlayerCount = _currentLobby.Players.Count,
                    PlayersReady = _currentLobby.Players.Count(p => p.IsReady),
                    WaitingPlayers = _registeredPlayers.Count(p => p.CurrentLobbyId == null),
                    TotalLobbiesCreated = _lobbies.Count,
                    FinishedLobbies = _lobbies.Count(l => l.State == LobbyState.Finished)
                };
            }
        }

        /// <summary>
        /// Get all registered players
        /// </summary>
        public List<Player> GetRegisteredPlayers()
        {
            lock (_lockObject)
            {
                return _registeredPlayers.ToList();
            }
        }

        /// <summary>
        /// Get all lobbies
        /// </summary>
        public List<Lobby> GetAllLobbies()
        {
            lock (_lockObject)
            {
                return _lobbies.ToList();
            }
        }

        /// <summary>
        /// Get number of waiting players (not in any lobby)
        /// </summary>
        public int GetWaitingPlayerCount()
        {
            lock (_lockObject)
            {
                return _registeredPlayers.Count(p => p.CurrentLobbyId == null);
            }
        }
    }

    /// <summary>
    /// Represents a player in the queue
    /// </summary>
    public class Player
    {
        public ulong UserId { get; set; }
        public string Username { get; set; }
        public string SteamId { get; set; }
        public string CurrentLobbyId { get; set; }
        public bool IsReady { get; set; }
        public DateTime RegisteredAt { get; set; }

        public override string ToString()
        {
            return $"{Username} ({SteamId}) - Ready: {IsReady} - Lobby: {CurrentLobbyId ?? "None"}";
        }
    }

    /// <summary>
    /// Represents a lobby
    /// </summary>
    public class Lobby
    {
        public string Id { get; set; }
        public List<Player> Players { get; set; } = new();
        public LobbyState State { get; set; }
        public string SteamAccountId { get; set; }  // Assigned when game ready
        public DateTime CreatedAt { get; set; }
        public DateTime? FinishedAt { get; set; }

        public int ReadyCount => Players.Count(p => p.IsReady);
        public int TotalCount => Players.Count;

        public override string ToString()
        {
            return $"Lobby: {Id} | State: {State} | Players: {ReadyCount}/{TotalCount} Ready | Account: {SteamAccountId ?? "None"}";
        }
    }

    /// <summary>
    /// Lobby states
    /// </summary>
    public enum LobbyState
    {
        Waiting,      // Waiting for players to join and ready
        Ready,        // All players ready, waiting for game to start
        InProgress,   // Game is running
        Finished      // Game finished
    }

    /// <summary>
    /// Queue statistics
    /// </summary>
    public class QueueStats
    {
        public int TotalRegisteredPlayers { get; set; }
        public Lobby CurrentLobby { get; set; }
        public int CurrentLobbyPlayerCount { get; set; }
        public int PlayersReady { get; set; }
        public int WaitingPlayers { get; set; }
        public int TotalLobbiesCreated { get; set; }
        public int FinishedLobbies { get; set; }

        public override string ToString()
        {
            return $"Queue: Registered={TotalRegisteredPlayers}, Waiting={WaitingPlayers}, " +
                   $"Current Lobby={CurrentLobbyPlayerCount}/{LobbyQueue.PLAYERS_PER_LOBBY} (Ready: {PlayersReady})";
        }
    }

    // Event Args
    public class LobbyCreatedEventArgs : EventArgs
    {
        public Lobby Lobby { get; set; }
    }

    public class PlayerJoinedEventArgs : EventArgs
    {
        public Player Player { get; set; }
        public Lobby Lobby { get; set; }
    }

    public class PlayerReadyEventArgs : EventArgs
    {
        public Player Player { get; set; }
    }

    public class LobbyFullEventArgs : EventArgs
    {
        public Lobby Lobby { get; set; }
    }

    public class LobbyReadyEventArgs : EventArgs
    {
        public Lobby Lobby { get; set; }
    }
}
