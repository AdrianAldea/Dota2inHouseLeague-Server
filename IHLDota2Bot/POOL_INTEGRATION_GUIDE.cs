using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IHLDota2Bot
{
    /// <summary>
    /// INTEGRATION GUIDE for Pool System into existing Program.cs
    /// Copy and paste code examples where indicated
    /// </summary>
    public partial class Program
    {
        // ====================  STEP 1: Add field to Program class ====================
        // Add this field to the Program class:
        // 
        // private LobbyPoolManager poolManager;
        //

        // ====================  STEP 2: Initialize in MainAsync ====================
        // In MainAsync(), after creating SteamBot, add:
        //
        // // Initialize pool system
        // poolManager = new LobbyPoolManager();
        //
        // // Register your Steam accounts
        // poolManager.RegisterSteamAccount("account1", "password1");
        // poolManager.RegisterSteamAccount("account2", "password2");
        // poolManager.RegisterSteamAccount("account3", "password3");
        // poolManager.RegisterSteamAccount("account4", "password4");
        //
        // // Subscribe to pool events
        // poolManager.GameReady += OnPoolGameReady;
        // poolManager.GameStarted += OnPoolGameStarted;
        // poolManager.GameFinished += OnPoolGameFinished;
        //

        // ====================  STEP 3: Modify RegisterPlayerAsync ====================
        // In RegisterPlayerAsync(), after SavePlayers(), add:
        //
        // // Register player in pool system
        // poolManager.RegisterPlayer(userId, message.Author.Username, steamId);
        // await message.Channel.SendMessageAsync($"✅ {player.Username} joined the queue! Current: {poolManager.GetQueueStats().CurrentLobbyPlayerCount}/10");
        //

        // ====================  STEP 4: Add new command handlers ====================
        // In HandleMessageAsync(), add these conditions:
        //
        // else if (content.StartsWith("!ready"))
        //     await HandleReadyAsync(message);
        // else if (content.StartsWith("!queue"))
        //     await ShowQueueStatusAsync(message);
        // else if (content.StartsWith("!pool"))
        //     await ShowPoolStatusAsync(message);
        // else if (content.StartsWith("!status"))
        //     await ShowSystemStatusAsync(message);
        // else if (content.StartsWith("!adminpool"))
        //     await ShowAdminPoolAsync(message);
        //

        // ====================  STEP 5: Implement new command handlers ====================

        // Copy these methods to your Program class:

        public async Task HandleReadyAsync(SocketMessage message)
        {
            try
            {
                var userId = message.Author.Id;
                
                if (!players.TryGetValue(userId, out var player))
                {
                    await message.Channel.SendMessageAsync("❌ You must register first with `!register <SteamID>`");
                    return;
                }

                poolManager.MarkPlayerReady(userId);
                var stats = poolManager.GetQueueStats();
                
                var embed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithTitle("✅ Ready Status Updated")
                    .WithDescription($"**{player.Username}** is now ready!\n\n" +
                        $"Current Lobby: **{stats.PlayersReady}/{stats.CurrentLobbyPlayerCount}** ready\n" +
                        (stats.PlayersReady == stats.CurrentLobbyPlayerCount && stats.CurrentLobbyPlayerCount == 10 
                            ? "🟢 **LOBBY READY TO START!**" 
                            : $"⏳ Waiting for {stats.CurrentLobbyPlayerCount - stats.PlayersReady} more ready"))
                    .WithTimestamp(DateTime.UtcNow)
                    .Build();

                await message.Channel.SendMessageAsync(embed: embed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleReadyAsync: {ex}");
                await message.Channel.SendMessageAsync("❌ Error marking you as ready");
            }
        }

        public async Task ShowQueueStatusAsync(SocketMessage message)
        {
            try
            {
                var stats = poolManager.GetQueueStats();
                var lobby = poolManager.GetCurrentLobby();
                
                var playerList = PoolExtensions.FormatPlayerList(lobby);
                
                var embed = new EmbedBuilder()
                    .WithColor(Color.Blue)
                    .WithTitle("👥 Queue Status")
                    .AddField("Registered Players", $"`{stats.TotalRegisteredPlayers}`", true)
                    .AddField("Waiting", $"`{stats.WaitingPlayers}`", true)
                    .AddField("Current Lobby", $"`{stats.CurrentLobbyPlayerCount}/10`", true)
                    .AddField("Lobby Progress", PoolExtensions.GetQueueProgressBar(stats.CurrentLobbyPlayerCount, 10), false)
                    .AddField("Ready Status", $"`{stats.PlayersReady}/{stats.CurrentLobbyPlayerCount}` ready", false)
                    .AddField("Players in Lobby", playerList, false)
                    .WithTimestamp(DateTime.UtcNow)
                    .Build();

                await message.Channel.SendMessageAsync(embed: embed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ShowQueueStatusAsync: {ex}");
                await message.Channel.SendMessageAsync("❌ Error showing queue status");
            }
        }

        public async Task ShowPoolStatusAsync(SocketMessage message)
        {
            try
            {
                var stats = poolManager.GetAccountPoolStats();
                var poolStatus = PoolExtensions.FormatPoolStatus(stats);
                
                var embed = new EmbedBuilder()
                    .WithColor(Color.Purple)
                    .WithTitle("📊 Steam Account Pool")
                    .WithDescription(poolStatus)
                    .WithTimestamp(DateTime.UtcNow)
                    .Build();

                await message.Channel.SendMessageAsync(embed: embed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ShowPoolStatusAsync: {ex}");
                await message.Channel.SendMessageAsync("❌ Error showing pool status");
            }
        }

        public async Task ShowSystemStatusAsync(SocketMessage message)
        {
            try
            {
                var status = poolManager.GetSystemStatus();
                var systemStatus = PoolExtensions.FormatSystemStatus(status);
                var poolStatus = PoolExtensions.FormatPoolStatus(status.PoolStats);
                var queueStatus = PoolExtensions.FormatQueueStatus(status.QueueStats);
                
                var embed = new EmbedBuilder()
                    .WithColor(Color.Gold)
                    .WithTitle("⚙️ Complete System Status")
                    .AddField(poolStatus.Split('\n')[0], string.Join("\n", poolStatus.Split('\n').Skip(1)), false)
                    .AddField(queueStatus.Split('\n')[0], string.Join("\n", queueStatus.Split('\n').Skip(1)), false)
                    .AddField(systemStatus.Split('\n')[0], string.Join("\n", systemStatus.Split('\n').Skip(1)), false)
                    .WithTimestamp(DateTime.UtcNow)
                    .Build();

                await message.Channel.SendMessageAsync(embed: embed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ShowSystemStatusAsync: {ex}");
                await message.Channel.SendMessageAsync("❌ Error showing system status");
            }
        }

        public async Task ShowAdminPoolAsync(SocketMessage message)
        {
            try
            {
                // Optional: Add admin role check here
                var accounts = poolManager.GetSteamAccounts();
                var accountList = PoolExtensions.FormatAccountList(accounts);
                
                var embed = new EmbedBuilder()
                    .WithColor(Color.Red)
                    .WithTitle("🔧 Admin: Account Pool Details")
                    .WithDescription(accountList)
                    .WithTimestamp(DateTime.UtcNow)
                    .Build();

                await message.Channel.SendMessageAsync(embed: embed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ShowAdminPoolAsync: {ex}");
                await message.Channel.SendMessageAsync("❌ Error showing admin pool");
            }
        }

        // ====================  STEP 6: Event handlers for pool events ====================
        // Add these event handler methods:

        private void OnPoolGameReady(object sender, GameReadyEventArgs e)
        {
            Console.WriteLine($"✅ GAME READY: Lobby {e.Lobby.Id} with account {e.Account.Username}");
            // Optional: Send Discord message
        }

        private void OnPoolGameStarted(object sender, GameStartedEventArgs e)
        {
            Console.WriteLine($"🎮 GAME STARTED: Lobby {e.Session.LobbyId}");
            // Optional: Send Discord message
        }

        private void OnPoolGameFinished(object sender, GameFinishedEventArgs e)
        {
            Console.WriteLine($"✅ GAME FINISHED: Lobby {e.Session.LobbyId}");
            // Optional: Send Discord message
            // New lobby will be auto-created
        }

        // ====================  STEP 7: Modify CreateGame command ====================
        // Replace the existing CreateGame method with:
        //
        // private async Task CreateGame(SocketMessage message)
        // {
        //     var lobby = poolManager.GetCurrentLobby();
        //     if (lobby == null || lobby.State != LobbyState.Ready)
        //     {
        //         await message.Channel.SendMessageAsync("❌ No lobby ready to play");
        //         return;
        //     }
        //
        //     var steamIds = lobby.Players.Select(p => p.SteamId).ToList();
        //     steamBot.CreateDotaLobby("D2IHLRO", "123", steamIds);
        //     
        //     poolManager.NotifyGameStarted(lobby.Id);
        //     await message.Channel.SendMessageAsync("🎮 Lobby created, invitations sent!");
        // }
        //

        // ====================  STEP 8: Add game finish command ====================
        // Add to HandleMessageAsync():
        //
        // else if (content.StartsWith("!finishgame"))
        //     await FinishGameAsync(message);
        //

        public async Task FinishGameAsync(SocketMessage message)
        {
            try
            {
                // Optional: Add admin check
                var activeSessions = poolManager.GetActiveSessions();
                
                if (activeSessions.Count == 0)
                {
                    await message.Channel.SendMessageAsync("❌ No active games");
                    return;
                }

                // Finish the last active game
                var session = activeSessions.Last();
                poolManager.NotifyGameFinished(session.LobbyId);

                await message.Channel.SendMessageAsync("✅ Game finished! Account released, new lobby created.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in FinishGameAsync: {ex}");
                await message.Channel.SendMessageAsync("❌ Error finishing game");
            }
        }

        // ====================  SUMMARY ====================
        // 
        // 1. Add poolManager field
        // 2. Initialize poolManager in MainAsync()
        // 3. Register steam accounts
        // 4. Subscribe to events
        // 5. Add pool registration to RegisterPlayerAsync()
        // 6. Add new command handlers to HandleMessageAsync()
        // 7. Implement command handlers
        // 8. Implement event handlers
        // 9. Modify CreateGame to use pool system
        // 10. Add game finish command
        //
        // THAT'S IT! Your pool system is now integrated! 🎉
        //
    }
}
