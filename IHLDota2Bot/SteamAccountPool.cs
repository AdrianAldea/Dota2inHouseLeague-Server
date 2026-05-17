using IHLDota2Bot.Clients;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IHLDota2Bot
{
    /// <summary>
    /// Manages a pool of Steam accounts for creating lobbies.
    /// Ensures accounts are properly recycled after use.
    /// </summary>
    public class SteamAccountPool
    {
        private List<SteamAccount> _accounts = new();
        private readonly object _lockObject = new object();

        private DiscordClient DiscordClient { get; }

        public SteamAccountPool(DiscordClient discordClient)
        {
            DiscordClient = discordClient;
        }

        /// <summary>
        /// Register a Steam account in the pool
        /// </summary>
        public void RegisterAccount(string username, string password)
        {
            lock (_lockObject)
            {
                if (_accounts.Any(a => a.Username == username))
                {
                    var existing = _accounts.First(a => a.Username == username);
                    var message = $"Account {username} already exists in pool with state {existing.State}";
                    DiscordClient.SendMessageAsync(message);
                    Console.WriteLine(message);
                    return;
                }

                var account = new SteamAccount
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = username,
                    Password = password,
                    State = AccountState.Ready,
                    RegisteredAt = DateTime.UtcNow,
                    LastUsedAt = null,
                    CurrentLobbyId = null
                };

                _accounts.Add(account);
                var successMessage = $"Account {username} registered successfully.";
                DiscordClient.SendMessageAsync(successMessage);
                Console.WriteLine(successMessage);
            }
        }

        /// <summary>
        /// Get the next available account from the pool
        /// </summary>
        public SteamAccount GetAvailableAccount()
        {
            lock (_lockObject)
            {
                var available = _accounts.FirstOrDefault(a => a.State == AccountState.Ready);

                if (available != null)
                {
                    available.State = AccountState.InUse;
                    var account = available as SteamAccount;

                    var message = $"Account {account.Username} is now in use.";
                    DiscordClient.SendMessageAsync(message);
                    Console.WriteLine(message);
                }
                else
                {
                    var message = "No available accounts in pool";
                    DiscordClient.SendMessageAsync(message);
                    Console.WriteLine(message);
                }

                return available;
            }
        }

        /// <summary>
        /// Get a specific account by username
        /// </summary>
        public SteamAccount GetAccountByUsername(string username)
        {
            lock (_lockObject)
            {
                return _accounts.FirstOrDefault(a => a.Username == username);
            }
        }

        /// <summary>
        /// Get a specific account by ID
        /// </summary>
        public SteamAccount GetAccountById(string accountId)
        {
            lock (_lockObject)
            {
                return _accounts.FirstOrDefault(a => a.Id == accountId);
            }
        }

        /// <summary>
        /// Release an account back to the pool (when game finishes)
        /// </summary>
        public void ReleaseAccount(string accountId)
        {
            lock (_lockObject)
            {
                var account = _accounts.FirstOrDefault(a => a.Id == accountId);
                if (account != null)
                {
                    account.State = AccountState.Ready;
                    account.LastUsedAt = DateTime.UtcNow;
                    account.CurrentLobbyId = null;
                    var message = $"Account {account.Username} released back to pool";
                    DiscordClient.SendMessageAsync(message);
                    Console.WriteLine(message);
                }
            }
        }

        /// <summary>
        /// Mark account as in use for a specific lobby
        /// </summary>
        public void MarkAccountInUse(string accountId, string lobbyId)
        {
            lock (_lockObject)
            {
                var account = _accounts.FirstOrDefault(a => a.Id == accountId);
                if (account != null)
                {
                    account.State = AccountState.InUse;
                    account.CurrentLobbyId = lobbyId;
                    account.LastUsedAt = DateTime.UtcNow;
                    var message = $"Account {account.Username} marked in use for lobby {lobbyId}";
                    DiscordClient.SendMessageAsync(message);
                    Console.WriteLine(message);
                }
            }
        }

        /// <summary>
        /// Get all accounts statistics
        /// </summary>
        public AccountPoolStats GetStats()
        {
            lock (_lockObject)
            {
                return new AccountPoolStats
                {
                    TotalAccounts = _accounts.Count,
                    ReadyAccounts = _accounts.Count(a => a.State == AccountState.Ready),
                    InUseAccounts = _accounts.Count(a => a.State == AccountState.InUse),
                    OnCooldownAccounts = _accounts.Count(a => a.State == AccountState.Cooldown),
                    Accounts = _accounts.ToList()
                };
            }
        }

        /// <summary>
        /// Check if there are available accounts
        /// </summary>
        public bool HasAvailableAccounts()
        {
            lock (_lockObject)
            {
                return _accounts.Any(a => a.State == AccountState.Ready);
            }
        }

        /// <summary>
        /// Get number of available accounts
        /// </summary>
        public int GetAvailableAccountCount()
        {
            lock (_lockObject)
            {
                return _accounts.Count(a => a.State == AccountState.Ready);
            }
        }

        /// <summary>
        /// Get all accounts
        /// </summary>
        public List<SteamAccount> GetAllAccounts()
        {
            lock (_lockObject)
            {
                return _accounts.ToList();
            }
        }
    }

    /// <summary>
    /// Represents a Steam account in the pool
    /// </summary>
    public class SteamAccount
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public AccountState State { get; set; }
        public DateTime RegisteredAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public string CurrentLobbyId { get; set; }
        //public SteamBot BotInstance { get; set; }

        public override string ToString()
        {
            return $"Account: {Username} | State: {State} | Lobby: {CurrentLobbyId ?? "None"} | Last Used: {LastUsedAt?.ToString("dd/MM/yyyy HH:mm") ?? "Never"}";
        }
    }

    /// <summary>
    /// Account states in the pool
    /// </summary>
    public enum AccountState
    {
        Ready,      // Available for use
        InUse,      // Currently hosting a lobby
        Cooldown    // Temporarily unavailable
    }

    /// <summary>
    /// Statistics about the account pool
    /// </summary>
    public class AccountPoolStats
    {
        public int TotalAccounts { get; set; }
        public int ReadyAccounts { get; set; }
        public int InUseAccounts { get; set; }
        public int OnCooldownAccounts { get; set; }
        public List<SteamAccount> Accounts { get; set; }

        public override string ToString()
        {
            return $"Pool Stats: Total={TotalAccounts}, Ready={ReadyAccounts}, InUse={InUseAccounts}, Cooldown={OnCooldownAccounts}";
        }
    }
}
