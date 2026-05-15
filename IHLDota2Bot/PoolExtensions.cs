using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IHLDota2Bot
{
    /// <summary>
    /// Helper methods for formatting pool system data for Discord
    /// </summary>
    public static class PoolExtensions
    {
        /// <summary>
        /// Format account pool stats for Discord display
        /// </summary>
        public static string FormatPoolStatus(AccountPoolStats stats)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📊 **STEAM ACCOUNT POOL**");
            sb.AppendLine($"├ Total: `{stats.TotalAccounts}`");
            sb.AppendLine($"├ Ready: `{stats.ReadyAccounts}` ✅");
            sb.AppendLine($"├ In Use: `{stats.InUseAccounts}` 🎮");
            sb.AppendLine($"└ Cooldown: `{stats.OnCooldownAccounts}` ⏳");
            return sb.ToString();
        }

        /// <summary>
        /// Format queue stats for Discord display
        /// </summary>
        public static string FormatQueueStatus(QueueStats stats)
        {
            var sb = new StringBuilder();
            sb.AppendLine("👥 **QUEUE STATUS**");
            sb.AppendLine($"├ Registered: `{stats.TotalRegisteredPlayers}`");
            sb.AppendLine($"├ Waiting: `{stats.WaitingPlayers}`");
            sb.AppendLine($"├ Current Lobby: `{stats.CurrentLobbyPlayerCount}/10`");
            sb.AppendLine($"├ Ready: `{stats.PlayersReady}/{stats.CurrentLobbyPlayerCount}`");
            sb.AppendLine($"└ Lobbies: `{stats.TotalLobbiesCreated}` (Finished: `{stats.FinishedLobbies}`)");
            return sb.ToString();
        }

        /// <summary>
        /// Format system status for Discord display
        /// </summary>
        public static string FormatSystemStatus(SystemStatus status)
        {
            var sb = new StringBuilder();
            sb.AppendLine("⚙️ **SYSTEM STATUS**");
            sb.AppendLine($"├ Active Games: `{status.ActiveSessions.Count}`");
            sb.AppendLine($"└ Updated: `{status.Timestamp:dd/MM/yyyy HH:mm:ss}`");
            return sb.ToString();
        }

        /// <summary>
        /// Create a progress bar for display
        /// </summary>
        public static string GetQueueProgressBar(int current, int total, int barLength = 10)
        {
            var filled = (int)((double)current / total * barLength);
            var bar = new string('█', filled) + new string('░', barLength - filled);
            return $"`[{bar}]` {current}/{total}";
        }

        /// <summary>
        /// Format player list for current lobby
        /// </summary>
        public static string FormatPlayerList(Lobby lobby)
        {
            if (lobby.Players.Count == 0)
                return "No players yet";

            var sb = new StringBuilder();
            foreach (var player in lobby.Players)
            {
                var status = player.IsReady ? "✅" : "⏳";
                sb.AppendLine($"{status} `{player.Username}`");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Format account details list
        /// </summary>
        public static string FormatAccountList(List<SteamAccount> accounts)
        {
            if (accounts.Count == 0)
                return "No accounts registered";

            var sb = new StringBuilder();
            foreach (var account in accounts)
            {
                var icon = account.State switch
                {
                    AccountState.Ready => "✅",
                    AccountState.InUse => "🎮",
                    AccountState.Cooldown => "⏳",
                    _ => "❓"
                };

                sb.AppendLine($"{icon} **{account.Username}**");
                sb.AppendLine($"   Status: `{account.State}`");
                if (account.CurrentLobbyId != null)
                    sb.AppendLine($"   Lobby: `{account.CurrentLobbyId}`");
                sb.AppendLine($"   Last Used: `{(account.LastUsedAt?.ToString("dd/MM/yyyy HH:mm") ?? "Never")}`");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Get emoji for account state
        /// </summary>
        public static string GetAccountStateEmoji(AccountState state)
        {
            return state switch
            {
                AccountState.Ready => "✅",
                AccountState.InUse => "🎮",
                AccountState.Cooldown => "⏳",
                _ => "❓"
            };
        }

        /// <summary>
        /// Get emoji for lobby state
        /// </summary>
        public static string GetLobbyStateEmoji(LobbyState state)
        {
            return state switch
            {
                LobbyState.Waiting => "⏳",
                LobbyState.Ready => "🟢",
                LobbyState.InProgress => "🎮",
                LobbyState.Finished => "✅",
                _ => "❓"
            };
        }

        /// <summary>
        /// Format time duration
        /// </summary>
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
                return $"{duration.Hours}h {duration.Minutes}m";
            else if (duration.TotalMinutes >= 1)
                return $"{duration.Minutes}m {duration.Seconds}s";
            else
                return $"{duration.Seconds}s";
        }

        /// <summary>
        /// Create a status bar with colors
        /// </summary>
        public static string CreateStatusBar(int current, int total)
        {
            const int barLength = 20;
            var percentage = (double)current / total;
            var filled = (int)(percentage * barLength);
            
            var bar = new string('█', filled) + new string('░', barLength - filled);
            var percentStr = (percentage * 100).ToString("F0");
            
            return $"`[{bar}]` {percentStr}% ({current}/{total})";
        }

        /// <summary>
        /// Truncate text to max length
        /// </summary>
        public static string Truncate(string text, int maxLength)
        {
            if (text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength - 3) + "...";
        }
    }
}
