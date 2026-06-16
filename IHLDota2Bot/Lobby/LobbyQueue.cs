using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Configuration;
using Microsoft.Extensions.Options;

namespace IHLDota2Bot;

/// <summary>
/// Manages the lobby queue and player registration.
///
/// Notification: this class is pure domain — it does not depend on Discord
/// (or any other UI/transport). It exposes events that subscribers (e.g.
/// <see cref="LobbyPoolManager"/>) translate into Discord broadcasts or any
/// other side effect. Tests can drive the queue without mocking I/O.
/// </summary>
public class LobbyQueue
{
    private readonly List<Player> registeredPlayers = [];
    private readonly List<Lobby> lobbies = [];
    private readonly Lock lockObject = new();
    private readonly ILogger<LobbyQueue> logger;
    private Lobby currentLobby;

    /// <summary>Players required to fill a lobby (configurable via <c>League:PlayersPerLobby</c>).</summary>
    public int PlayersPerLobby { get; }

    public event EventHandler<LobbyCreatedEventArgs>? LobbyCreated;
    public event EventHandler<PlayerJoinedEventArgs>? PlayerJoined;
    public event EventHandler<PlayerLeftEventArgs>? PlayerLeft;
    public event EventHandler<PlayerReadyEventArgs>? PlayerReady;
    public event EventHandler<PlayerUnreadyEventArgs>? PlayerUnready;
    public event EventHandler<PlayerKickedEventArgs>? PlayerKicked;
    public event EventHandler<LobbyFullEventArgs>? LobbyFull;
    public event EventHandler<LobbyReadyEventArgs>? LobbyReady;
    public event EventHandler<LobbyResetEventArgs>? LobbyReset;
    public event EventHandler<LobbyFinishedEventArgs>? LobbyFinished;

    public LobbyQueue(ILogger<LobbyQueue> logger, IOptions<LeagueOptions> leagueOptions)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(leagueOptions);

        this.logger = logger;
        PlayersPerLobby = leagueOptions.Value.PlayersPerLobby;
        currentLobby = CreateLobbyUnlocked();
    }

    public Task<QueueOperationResponse> JoinQueueAsync(ulong userId, string username, string steamId)
    {
        bool added = false;
        bool lobbyFull = false;
        Player? player;
        string message;

        lock (lockObject)
        {
            player = registeredPlayers.FirstOrDefault(p => p.UserId == userId);
            if (player == null)
            {
                player = new Player
                {
                    UserId = userId,
                    Username = username,
                    SteamId = steamId,
                    RegisteredAt = DateTime.UtcNow
                };
                registeredPlayers.Add(player);
            }
            else
            {
                player.Username = username;
                player.SteamId = steamId;
            }

            if (player.CurrentLobbyId != null)
            {
                message = $"{username} is already in lobby {player.CurrentLobbyId} ({currentLobby.Players.Count}/{PlayersPerLobby})";
            }
            else if (currentLobby.Players.Count >= PlayersPerLobby)
            {
                message = $"Current lobby is full ({currentLobby.Players.Count}/{PlayersPerLobby})";
            }
            else
            {
                currentLobby.Players.Add(player);
                player.CurrentLobbyId = currentLobby.Id;
                player.IsReady = false;
                added = true;

                if (currentLobby.Players.Count == PlayersPerLobby)
                {
                    currentLobby.State = LobbyState.Waiting;
                    lobbyFull = true;
                }

                message = $"{username} joined the queue ({currentLobby.Players.Count}/{PlayersPerLobby})";
            }
        }

        if (added)
            PlayerJoined?.Invoke(this, new PlayerJoinedEventArgs { Player = player, Lobby = currentLobby });

        if (lobbyFull)
            LobbyFull?.Invoke(this, new LobbyFullEventArgs { Lobby = currentLobby });

        logger.LogInformation("{Message}", message);
        return Task.FromResult(new QueueOperationResponse { Success = added, Message = message });
    }

    public Task<QueueOperationResponse> LeaveQueueAsync(ulong userId)
    {
        bool removed = false;
        Player? player;
        Lobby? lobby = null;
        string message;

        lock (lockObject)
        {
            player = registeredPlayers.FirstOrDefault(p => p.UserId == userId);
            if (player == null || player.CurrentLobbyId == null)
            {
                message = "You are not in the queue";
            }
            else
            {
                lobby = lobbies.FirstOrDefault(l => l.Id == player.CurrentLobbyId);
                lobby?.Players.Remove(player);
                player.CurrentLobbyId = null;
                player.IsReady = false;
                removed = true;
                message = $"{player.Username} left the queue ({currentLobby.Players.Count}/{PlayersPerLobby})";
            }
        }

        if (removed && player != null && lobby != null)
            PlayerLeft?.Invoke(this, new PlayerLeftEventArgs { Player = player, Lobby = lobby });

        logger.LogInformation("{Message}", message);
        return Task.FromResult(new QueueOperationResponse { Success = removed, Message = message });
    }

    public Task<QueueOperationResponse> MarkPlayerUnreadyAsync(ulong userId)
    {
        bool changed = false;
        Player? player;
        string message;

        lock (lockObject)
        {
            player = registeredPlayers.FirstOrDefault(p => p.UserId == userId);
            if (player == null || player.CurrentLobbyId == null)
            {
                message = "You are not in a lobby";
            }
            else if (!player.IsReady)
            {
                message = $"{player.Username} is already not ready";
            }
            else
            {
                player.IsReady = false;
                changed = true;
                message = $"{player.Username} is no longer ready";
            }
        }

        if (changed && player != null)
            PlayerUnready?.Invoke(this, new PlayerUnreadyEventArgs { Player = player });

        logger.LogInformation("{Message}", message);
        return Task.FromResult(new QueueOperationResponse { Success = changed, Message = message });
    }

    public Task<QueueOperationResponse> KickFromLobbyAsync(ulong userId)
    {
        bool kicked = false;
        Player? player;
        Lobby? lobby = null;
        string message;

        lock (lockObject)
        {
            player = registeredPlayers.FirstOrDefault(p => p.UserId == userId);
            if (player == null || player.CurrentLobbyId == null)
            {
                message = "User is not in a lobby.";
            }
            else
            {
                lobby = lobbies.FirstOrDefault(l => l.Id == player.CurrentLobbyId);
                lobby?.Players.Remove(player);
                player.CurrentLobbyId = null;
                player.IsReady = false;
                kicked = true;
                message = $"{player.Username} was kicked from the lobby ({currentLobby.Players.Count}/{PlayersPerLobby}).";
            }
        }

        if (kicked && player != null && lobby != null)
            PlayerKicked?.Invoke(this, new PlayerKickedEventArgs { Player = player, Lobby = lobby });

        logger.LogInformation("{Message}", message);
        return Task.FromResult(new QueueOperationResponse { Success = kicked, Message = message });
    }

    public Task<QueueOperationResponse> ForceLobbyReadyAsync()
    {
        Lobby lobby;
        int count;

        lock (lockObject)
        {
            if (currentLobby.Players.Count == 0)
                return Task.FromResult(new QueueOperationResponse { Success = false, Message = "Current lobby is empty." });

            foreach (var p in currentLobby.Players) p.IsReady = true;
            currentLobby.State = LobbyState.Ready;
            lobby = currentLobby;
            count = lobby.Players.Count;
        }

        LobbyReady?.Invoke(this, new LobbyReadyEventArgs { Lobby = lobby });

        var message = $"Lobby {lobby.Id} force-started with {count} player(s).";
        logger.LogInformation("{Message}", message);
        return Task.FromResult(new QueueOperationResponse { Success = true, Message = message });
    }

    public Task<QueueOperationResponse> ResetCurrentLobbyAsync()
    {
        int count;
        Lobby resetLobby;

        lock (lockObject)
        {
            count = currentLobby.Players.Count;
            foreach (var p in currentLobby.Players)
            {
                p.CurrentLobbyId = null;
                p.IsReady = false;
            }
            currentLobby.Players.Clear();
            currentLobby.State = LobbyState.Waiting;
            resetLobby = currentLobby;
        }

        LobbyReset?.Invoke(this, new LobbyResetEventArgs { Lobby = resetLobby, RemovedPlayerCount = count });

        var message = $"Queue reset. {count} player(s) removed from the current lobby.";
        logger.LogInformation("{Message}", message);
        return Task.FromResult(new QueueOperationResponse { Success = true, Message = message });
    }

    public bool RemovePlayerCompletely(ulong userId)
    {
        lock (lockObject)
        {
            var player = registeredPlayers.FirstOrDefault(p => p.UserId == userId);
            if (player == null) return false;

            if (player.CurrentLobbyId != null)
            {
                var lobby = lobbies.FirstOrDefault(l => l.Id == player.CurrentLobbyId);
                lobby?.Players.Remove(player);
            }

            registeredPlayers.Remove(player);
            return true;
        }
    }

    public bool UpdatePlayerSteamId(ulong userId, string steamId)
    {
        lock (lockObject)
        {
            var player = registeredPlayers.FirstOrDefault(p => p.UserId == userId);
            if (player == null) return false;
            player.SteamId = steamId;
            return true;
        }
    }

    public QueueStatusResponse GetQueueStatus()
    {
        lock (lockObject)
        {
            return new QueueStatusResponse
            {
                CurrentLobbyId = currentLobby.Id,
                CurrentLobbyPlayerCount = currentLobby.Players.Count,
                PlayersPerLobby = PlayersPerLobby,
                PlayersReady = currentLobby.Players.Count(p => p.IsReady),
                TotalRegistered = registeredPlayers.Count,
                WaitingPlayers = registeredPlayers.Count(p => p.CurrentLobbyId == null),
                Players = currentLobby.Players
                    .Select(p => new QueuePlayerSummary { Username = p.Username, IsReady = p.IsReady })
                    .ToList()
            };
        }
    }

    public Task<QueueOperationResponse> MarkPlayerReadyAsync(ulong userId)
    {
        Player? player;
        Lobby? readyLobby = null;
        bool changed = false;
        string message;

        lock (lockObject)
        {
            player = registeredPlayers.FirstOrDefault(p => p.UserId == userId);
            if (player == null || player.CurrentLobbyId == null)
            {
                message = "You are not in a lobby";
            }
            else if (player.IsReady)
            {
                message = $"{player.Username} is already ready";
            }
            else
            {
                player.IsReady = true;
                changed = true;
                if (CheckLobbyAllReadyUnlocked(player.CurrentLobbyId))
                {
                    readyLobby = lobbies.FirstOrDefault(l => l.Id == player.CurrentLobbyId);
                    if (readyLobby != null)
                        readyLobby.State = LobbyState.Ready;
                }
                message = $"{player.Username} marked as ready";
            }
        }

        if (changed)
            PlayerReady?.Invoke(this, new PlayerReadyEventArgs { Player = player! });

        logger.LogInformation("{Message}", message);

        if (readyLobby != null)
        {
            LobbyReady?.Invoke(this, new LobbyReadyEventArgs { Lobby = readyLobby });
            logger.LogInformation("Lobby {LobbyId} is READY! All players ready.", readyLobby.Id);
        }

        return Task.FromResult(new QueueOperationResponse { Success = changed, Message = message });
    }

    private bool CheckLobbyAllReadyUnlocked(string lobbyId)
    {
        var lobby = lobbies.FirstOrDefault(l => l.Id == lobbyId);
        if (lobby == null) return false;
        return lobby.Players.Count == PlayersPerLobby && lobby.Players.All(p => p.IsReady);
    }

    public Task FinishLobbyAsync(string lobbyId)
    {
        Lobby? lobby;
        Lobby? newLobby = null;

        lock (lockObject)
        {
            lobby = lobbies.FirstOrDefault(l => l.Id == lobbyId);
            if (lobby != null)
            {
                lobby.State = LobbyState.Finished;
                lobby.FinishedAt = DateTime.UtcNow;

                foreach (var player in lobby.Players)
                {
                    player.CurrentLobbyId = null;
                    player.IsReady = false;
                }

                newLobby = CreateLobbyUnlocked();
                currentLobby = newLobby;
            }
        }

        if (lobby == null) return Task.CompletedTask;

        LobbyFinished?.Invoke(this, new LobbyFinishedEventArgs { Lobby = lobby });
        logger.LogInformation("Lobby {LobbyId} has finished.", lobby.Id);

        if (newLobby != null)
        {
            LobbyCreated?.Invoke(this, new LobbyCreatedEventArgs { Lobby = newLobby });
            logger.LogInformation("New lobby created: {LobbyId} with {Count} players", newLobby.Id, newLobby.Players.Count);

            if (newLobby.Players.Count == PlayersPerLobby)
                LobbyFull?.Invoke(this, new LobbyFullEventArgs { Lobby = newLobby });
        }

        return Task.CompletedTask;
    }

    private Lobby CreateLobbyUnlocked()
    {
        var lobby = new Lobby
        {
            Id = Guid.NewGuid().ToString(),
            Players = [],
            State = LobbyState.Waiting,
            CreatedAt = DateTime.UtcNow
        };
        lobbies.Add(lobby);

        var availablePlayers = registeredPlayers
            .Where(p => p.CurrentLobbyId == null)
            .Take(PlayersPerLobby)
            .ToList();

        foreach (var player in availablePlayers)
        {
            lobby.Players.Add(player);
            player.CurrentLobbyId = lobby.Id;
            player.IsReady = false;
        }

        return lobby;
    }

    public Lobby GetCurrentLobby()
    {
        lock (lockObject)
        {
            return currentLobby;
        }
    }

    public Lobby? GetLobbyById(string lobbyId)
    {
        lock (lockObject)
        {
            return lobbies.FirstOrDefault(l => l.Id == lobbyId);
        }
    }

    public QueueStats GetStats()
    {
        lock (lockObject)
        {
            return new QueueStats
            {
                TotalRegisteredPlayers = registeredPlayers.Count,
                CurrentLobby = currentLobby,
                PlayersPerLobby = PlayersPerLobby,
                CurrentLobbyPlayerCount = currentLobby.Players.Count,
                PlayersReady = currentLobby.Players.Count(p => p.IsReady),
                WaitingPlayers = registeredPlayers.Count(p => p.CurrentLobbyId == null),
                TotalLobbiesCreated = lobbies.Count,
                FinishedLobbies = lobbies.Count(l => l.State == LobbyState.Finished)
            };
        }
    }

    public List<Player> GetRegisteredPlayers()
    {
        lock (lockObject)
        {
            return registeredPlayers.ToList();
        }
    }

    public List<Lobby> GetAllLobbies()
    {
        lock (lockObject)
        {
            return lobbies.ToList();
        }
    }

    public int GetWaitingPlayerCount()
    {
        lock (lockObject)
        {
            return registeredPlayers.Count(p => p.CurrentLobbyId == null);
        }
    }
}
