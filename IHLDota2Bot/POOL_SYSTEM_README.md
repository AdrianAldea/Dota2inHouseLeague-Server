# Pool System for IHL Dota 2 Bot

A complete **Steam Account Pool** and **Player Queue** system for managing 10-player Dota 2 lobbies with automatic account rotation.

## 🎯 Overview

The pool system automates the entire lifecycle of creating 10-player Dota 2 lobbies:

1. **Players register** → Auto-added to queue
2. **10 players join** → Lobby created automatically  
3. **All players ready** → Assigned a Steam account from the pool
4. **Game finishes** → Account released back to pool
5. **Repeat** → New lobby created automatically

## 📦 Components

### 1. **SteamAccountPool.cs**
Manages 4+ Steam accounts with pooling:
- Register accounts with credentials
- Track state: `Ready`, `InUse`, `Cooldown`
- Automatic assignment to lobbies
- Thread-safe operations

```csharp
// Usage
poolManager.RegisterSteamAccount("account1", "password1");
poolManager.RegisterSteamAccount("account2", "password2");
poolManager.RegisterSteamAccount("account3", "password3");
poolManager.RegisterSteamAccount("account4", "password4");
```

### 2. **LobbyQueue.cs**
Manages player registration and lobbies:
- Register players (20 total)
- Automatic lobby creation (10 players per lobby)
- Track ready status
- Events: `LobbyCreated`, `LobbyFull`, `LobbyReady`, `PlayerJoined`, `PlayerReady`

```csharp
// Usage
poolManager.RegisterPlayer(userId, username, steamId);
poolManager.MarkPlayerReady(userId);
```

### 3. **LobbyPoolManager.cs**
Main orchestrator that ties everything together:
- Manages account pool & lobby queue
- Assigns accounts to ready lobbies
- Tracks active game sessions
- Events: `GameReady`, `GameStarted`, `GameFinished`

```csharp
var poolManager = new LobbyPoolManager();
poolManager.RegisterSteamAccount("account1", "password1");
poolManager.RegisterPlayer(userId, username, steamId);
poolManager.NotifyGameFinished(lobbyId);
```

### 4. **PoolExtensions.cs**
Discord formatting and helper methods:
- Format pool status for Discord
- Format queue status for Discord  
- Format system status for Discord
- Helper functions for Discord embeds

```csharp
// Usage
var poolStatus = PoolExtensions.FormatPoolStatus(stats);
var queueStatus = PoolExtensions.FormatQueueStatus(stats);
```

## 🔄 System Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                     POOL SYSTEM FLOW                             │
└─────────────────────────────────────────────────────────────────┘

STEP 1: PLAYER REGISTRATION
  Players: !register <SteamID>
  → Auto-added to pool queue
  → Added to waiting queue

STEP 2: LOBBY CREATION
  Condition: 10 players ready to join
  → LobbyQueue creates new lobby
  → Auto-fills with first 10 waiting players
  → Players added to lobby

STEP 3: PLAYERS GET READY
  Players: !ready
  → MarkPlayerReady(userId)
  → Check if all players ready
  → If yes → emit LobbyReady event

STEP 4: GAME READY
  Event: LobbyReady triggered
  → LobbyPoolManager: Get available Steam account
  → Assign account to lobby
  → Create LobbySession
  → Emit GameReady event
  → Ready to create Dota 2 lobby!

STEP 5: GAME STARTS
  Admin: Creates Dota 2 lobby with assigned account
  → NotifyGameStarted(lobbyId)
  → Session state: InProgress
  → Emit GameStarted event

STEP 6: GAME FINISHES
  Admin: !x finishgame <lobbyId>
  → NotifyGameFinished(lobbyId)
  → Release Steam account back to pool
  → Session state: Finished
  → Emit GameFinished event
  → Mark lobby as finished
  → Create new lobby automatically
  → Auto-fill with waiting players
  → Back to STEP 3!
```

## 🎮 Discord Commands

```
!register <SteamID>     - Register to the system
!ready                  - Mark yourself ready in current lobby
!queue                  - Show queue status
!pool                   - Show Steam account pool status
!status                 - Show full system status
!mylobby                - Show your current lobby
!lobby                  - Show current lobby details

[ADMIN]
!adminpool              - Show detailed pool info
!adminstatus            - Show detailed system status
```

## 📊 Data Structures

### SteamAccount
```csharp
public class SteamAccount
{
    public string Id { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public AccountState State { get; set; }  // Ready, InUse, Cooldown
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string CurrentLobbyId { get; set; }
}
```

### Lobby
```csharp
public class Lobby
{
    public string Id { get; set; }
    public List<Player> Players { get; set; }
    public LobbyState State { get; set; }    // Waiting, Ready, InProgress, Finished
    public DateTime CreatedAt { get; set; }
    public string SteamAccountId { get; set; }
}
```

### LobbySession
```csharp
public class LobbySession
{
    public string LobbyId { get; set; }
    public string AccountId { get; set; }
    public List<Player> Players { get; set; }
    public LobbySessionState State { get; set; }  // Ready, InProgress, Finished
    public DateTime StartedAt { get; set; }
    public DateTime? GameStartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
```

## 🔌 Integration Steps

### 1. Add to Program.cs (MainAsync)
```csharp
// Initialize pool system
poolManager = new LobbyPoolManager();

// Register your Steam accounts
poolManager.RegisterSteamAccount("account1", "password1");
poolManager.RegisterSteamAccount("account2", "password2");
poolManager.RegisterSteamAccount("account3", "password3");
poolManager.RegisterSteamAccount("account4", "password4");

// Subscribe to events
poolManager.GameReady += OnGameReady;
poolManager.GameStarted += OnGameStarted;
poolManager.GameFinished += OnGameFinished;
```

### 2. Modify RegisterPlayerAsync
```csharp
// After SavePlayers(), add:
poolManager.RegisterPlayer(userId, message.Author.Username, steamId);
```

### 3. Add Command Handlers
```csharp
else if (content.StartsWith("!ready"))
    await HandleReadyAsync(message);
else if (content.StartsWith("!queue"))
    await ShowQueueStatusAsync(message);
else if (content.StartsWith("!pool"))
    await ShowPoolStatusAsync(message);
else if (content.StartsWith("!status"))
    await ShowSystemStatusAsync(message);
```

### 4. Modify CreateGame Command
```csharp
var lobby = poolManager.GetCurrentLobby();
var account = poolManager.GetSteamAccounts()
    .FirstOrDefault(a => a.Id == lobby.SteamAccountId);

steamBot.CreateDotaLobby("D2IHLRO", "123", steamIds);
poolManager.NotifyGameStarted(lobby.Id);
```

### 5. When Game Finishes
```csharp
poolManager.NotifyGameFinished(lobbyId);
// System automatically:
// - Releases Steam account back to pool
// - Creates new lobby
// - Auto-fills with waiting players
```

## 📈 Scaling

You can easily scale by adding more Steam accounts:

```csharp
// Add 5 accounts
for (int i = 1; i <= 5; i++)
{
    poolManager.RegisterSteamAccount($"steam_account_{i}", $"password_{i}");
}
```

With N accounts:
- Can run N concurrent games
- N lobbies can be in-progress simultaneously
- New lobbies created automatically for waiting players

## ⚙️ Configuration

Edit `LobbyQueue.cs`:
```csharp
// Change players per lobby
public const int PLAYERS_PER_LOBBY = 10;  // Change to different number
```

## 🔒 Thread Safety

All components use thread-safe locks:
- `SteamAccountPool`: Lock on account list modifications
- `LobbyQueue`: Lock on player/lobby list modifications
- `LobbyPoolManager`: Lock on session management

## 📝 Events

```csharp
// Account pool events
poolManager.GameReady += (sender, args) => {
    // Ready to start game with args.Session
};

poolManager.GameStarted += (sender, args) => {
    // Game started for args.Session
};

poolManager.GameFinished += (sender, args) => {
    // Game finished for args.Session
};
```

## 🐛 Debugging

Print system status:
```csharp
poolManager.PrintSystemStatus();

// Output:
// ╔════════════════════════════════════════════════════════════╗
// ║          POOL SYSTEM STATUS                              ║
// ╚════════════════════════════════════════════════════════════╝
//
// 📊 STEAM ACCOUNT POOL:
//    Total Accounts: 4
//    Ready: 2
//    In Use: 1
//    On Cooldown: 1
// ... etc
```

## 📊 Get Statistics

```csharp
// Account pool stats
var poolStats = poolManager.GetAccountPoolStats();
Console.WriteLine(poolStats);  // Shows: Total=4, Ready=2, InUse=1, Cooldown=1

// Queue stats
var queueStats = poolManager.GetQueueStats();
Console.WriteLine(queueStats);  // Shows player and lobby counts

// System status
var status = poolManager.GetSystemStatus();
// Full system snapshot with all data

// Active sessions
var sessions = poolManager.GetActiveSessions();
// List of currently running games
```

## 🚀 Example: Complete Workflow

```csharp
// 1. Initialize
var poolManager = new LobbyPoolManager();
poolManager.RegisterSteamAccount("acc1", "pass1");
poolManager.RegisterSteamAccount("acc2", "pass2");

// 2. Player registers
poolManager.RegisterPlayer(123456, "PlayerName", "76561198012345678");
// Auto-added to queue, waiting to join lobby

// 3. 9 more players register
// (repeat step 2)

// 4. Check status
var stats = poolManager.GetQueueStats();
// Current Lobby: 10/10 (Ready: 0)

// 5. All players ready
for (int i = 0; i < 10; i++)
{
    poolManager.MarkPlayerReady(playerIds[i]);
}

// 6. Check status
stats = poolManager.GetQueueStats();
// Current Lobby: 10/10 (Ready: 10) - GAME READY!

// 7. Create Dota lobby
var lobby = poolManager.GetCurrentLobby();
steamBot.CreateDotaLobby("D2IHLRO", "123", lobby.Players.Select(p => p.SteamId).ToList());
poolManager.NotifyGameStarted(lobby.Id);

// 8. Game runs...
// (players play Dota 2)

// 9. Game finishes
poolManager.NotifyGameFinished(lobby.Id);

// 10. System automatically:
// - Releases Steam account back to pool
// - Creates new lobby for next batch
// - If players waiting, auto-fills lobby

// 11. Back to step 4! ♻️
```

## ✅ Checklist

The integration below is complete. Some pieces landed under different names
than this design doc originally proposed: the pool lives in
`Lobby/LobbyPoolManager.cs` + `Lobby/LobbyQueue.cs` with the Steam account
pool in `Services/SteamGcClientPool.cs`, and the Discord commands are handled
by the `IHLDota2Bot.Discord` service (available as both `!` text commands and
slash commands).

- [x] Add the pool/queue/manager files to the project
- [x] Wire the pool manager up at startup (DI-registered, not MainAsync)
- [x] Register Steam accounts (config + `!add_account` admin command)
- [x] Player registration flows through the pool manager
- [x] Add the Discord commands (`!ready`, `!queue`, `!pool_status`, `!status`)
- [x] Subscribe to pool events (GameReady, GameStarted, GameFinished)
- [x] Game creation uses the pool system
- [x] Add game finish handler
- [x] Test registration and queue filling
- [x] Test ready status and game readiness
- [x] Test game lifecycle and account release (see `LobbyPoolManager*Tests`)

## 🎉 You're Ready!

The pool system is now fully integrated. Your Dota 2 IHL bot can now:
- Manage 20+ registered players
- Auto-create 10-player lobbies
- Rotate through 4+ Steam accounts
- Automatically handle game lifecycles
- Continuously run new lobbies without manual intervention!

Enjoy your in-house league! 🎮
