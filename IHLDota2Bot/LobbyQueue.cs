using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot;

/// <summary>
/// Manages the lobby queue and player registration.
/// </summary>
public class LobbyQueue
{
    public const int PlayersPerLobby = 10;

    private readonly List<Player> registeredPlayers = [];
    private readonly List<Lobby> lobbies = [];
    private readonly Lock lockObject = new();
    private readonly DiscordClient discordClient;
    private readonly ILogger<LobbyQueue> logger;
    private Lobby currentLobby;

    public event EventHandler<LobbyCreatedEventArgs>? LobbyCreated;
    public event EventHandler<PlayerJoinedEventArgs>? PlayerJoined;
    public event EventHandler<PlayerReadyEventArgs>? PlayerReady;
    public event EventHandler<LobbyFullEventArgs>? LobbyFull;
    public event EventHandler<LobbyReadyEventArgs>? LobbyReady;

    public LobbyQueue(DiscordClient discordClient, ILogger<LobbyQueue> logger)
    {
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(logger);

        this.discordClient = discordClient;
        this.logger = logger;
        currentLobby = CreateLobbyUnlocked();
    }

    public async Task RegisterPlayerAsync(ulong userId, string username, string steamId)
    {
        string message;
        bool lobbyFull = false;
        Player? joinedPlayer = null;

        lock (lockObject)
        {
            if (registeredPlayers.Any(p => p.UserId == userId))
            {
                message = $"Player {username} already registered";
            }
            else
            {
                var player = new Player
                {
                    UserId = userId,
                    Username = username,
                    SteamId = steamId,
                    RegisteredAt = DateTime.UtcNow
                };
                registeredPlayers.Add(player);

                if (currentLobby.Players.Count < PlayersPerLobby)
                {
                    currentLobby.Players.Add(player);
                    player.CurrentLobbyId = currentLobby.Id;
                    joinedPlayer = player;

                    if (currentLobby.Players.Count == PlayersPerLobby)
                    {
                        currentLobby.State = LobbyState.Waiting;
                        lobbyFull = true;
                    }
                }

                message = $"Player {username} registered. Queue: {currentLobby.Players.Count}/{PlayersPerLobby}";
            }
        }

        if (joinedPlayer != null)
            PlayerJoined?.Invoke(this, new PlayerJoinedEventArgs { Player = joinedPlayer, Lobby = currentLobby });

        if (lobbyFull)
            LobbyFull?.Invoke(this, new LobbyFullEventArgs { Lobby = currentLobby });

        logger.LogInformation("{Message}", message);
        await discordClient.SendMessageAsync(message);
    }

    public async Task<QueueOperationResponse> JoinQueueAsync(ulong userId, string username, string steamId)
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
        await discordClient.SendMessageAsync(message);

        return new QueueOperationResponse { Success = added, Message = message };
    }

    public async Task<QueueOperationResponse> LeaveQueueAsync(ulong userId)
    {
        bool removed = false;
        string message;

        lock (lockObject)
        {
            var player = registeredPlayers.FirstOrDefault(p => p.UserId == userId);
            if (player == null || player.CurrentLobbyId == null)
            {
                message = $"You are not in the queue";
            }
            else
            {
                var lobby = lobbies.FirstOrDefault(l => l.Id == player.CurrentLobbyId);
                lobby?.Players.Remove(player);
                player.CurrentLobbyId = null;
                player.IsReady = false;
                removed = true;
                message = $"{player.Username} left the queue ({currentLobby.Players.Count}/{PlayersPerLobby})";
            }
        }

        logger.LogInformation("{Message}", message);
        await discordClient.SendMessageAsync(message);

        return new QueueOperationResponse { Success = removed, Message = message };
    }

    public async Task<QueueOperationResponse> MarkPlayerUnreadyAsync(ulong userId)
    {
        bool changed = false;
        string message;

        lock (lockObject)
        {
            var player = registeredPlayers.FirstOrDefault(p => p.UserId == userId);
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

        logger.LogInformation("{Message}", message);
        await discordClient.SendMessageAsync(message);

        return new QueueOperationResponse { Success = changed, Message = message };
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

    public async Task<QueueOperationResponse> MarkPlayerReadyAsync(ulong userId)
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
        await discordClient.SendMessageAsync(message);

        if (readyLobby != null)
        {
            LobbyReady?.Invoke(this, new LobbyReadyEventArgs { Lobby = readyLobby });
            var readyMessage = $"Lobby {readyLobby.Id} is READY! All players ready.";
            logger.LogInformation("{Message}", readyMessage);
            await discordClient.SendMessageAsync(readyMessage);
        }

        return new QueueOperationResponse { Success = changed, Message = message };
    }

    private bool CheckLobbyAllReadyUnlocked(string lobbyId)
    {
        var lobby = lobbies.FirstOrDefault(l => l.Id == lobbyId);
        if (lobby == null) return false;
        return lobby.Players.Count == PlayersPerLobby && lobby.Players.All(p => p.IsReady);
    }

    public async Task FinishLobbyAsync(string lobbyId)
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

        if (lobby == null) return;

        var finishedMessage = $"Lobby {lobby.Id} has finished. Players can now join the next lobby.";
        logger.LogInformation("{Message}", finishedMessage);
        await discordClient.SendMessageAsync(finishedMessage);

        if (newLobby != null)
        {
            LobbyCreated?.Invoke(this, new LobbyCreatedEventArgs { Lobby = newLobby });
            var createdMessage = $"New lobby created: {newLobby.Id} with {newLobby.Players.Count} players";
            logger.LogInformation("{Message}", createdMessage);
            await discordClient.SendMessageAsync(createdMessage);

            if (newLobby.Players.Count == PlayersPerLobby)
                LobbyFull?.Invoke(this, new LobbyFullEventArgs { Lobby = newLobby });
        }
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

public class Player
{
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string SteamId { get; set; } = string.Empty;
    public string? CurrentLobbyId { get; set; }
    public bool IsReady { get; set; }
    public DateTime RegisteredAt { get; set; }

    public override string ToString() =>
        $"{Username} ({SteamId}) - Ready: {IsReady} - Lobby: {CurrentLobbyId ?? "None"}";
}

public class Lobby
{
    public string Id { get; set; } = string.Empty;
    public List<Player> Players { get; set; } = [];
    public LobbyState State { get; set; }
    public string? SteamAccountId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }

    public int ReadyCount => Players.Count(p => p.IsReady);
    public int TotalCount => Players.Count;

    public override string ToString() =>
        $"Lobby: {Id} | State: {State} | Players: {ReadyCount}/{TotalCount} Ready | Account: {SteamAccountId ?? "None"}";
}

public enum LobbyState
{
    Waiting,
    Ready,
    InProgress,
    Finished
}

public class QueueStats
{
    public int TotalRegisteredPlayers { get; set; }
    public Lobby? CurrentLobby { get; set; }
    public int CurrentLobbyPlayerCount { get; set; }
    public int PlayersReady { get; set; }
    public int WaitingPlayers { get; set; }
    public int TotalLobbiesCreated { get; set; }
    public int FinishedLobbies { get; set; }

    public override string ToString() =>
        $"Queue: Registered={TotalRegisteredPlayers}, Waiting={WaitingPlayers}, " +
        $"Current Lobby={CurrentLobbyPlayerCount}/{LobbyQueue.PlayersPerLobby} (Ready: {PlayersReady})";
}

public class LobbyCreatedEventArgs : EventArgs
{
    public required Lobby Lobby { get; set; }
}

public class PlayerJoinedEventArgs : EventArgs
{
    public required Player Player { get; set; }
    public required Lobby Lobby { get; set; }
}

public class PlayerReadyEventArgs : EventArgs
{
    public required Player Player { get; set; }
}

public class LobbyFullEventArgs : EventArgs
{
    public required Lobby Lobby { get; set; }
}

public class LobbyReadyEventArgs : EventArgs
{
    public required Lobby Lobby { get; set; }
}
