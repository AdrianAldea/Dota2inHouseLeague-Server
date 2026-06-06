using IHLDota2Bot.Clients;

namespace IHLDota2Bot;

/// <summary>
/// Manages a pool of Steam accounts for creating lobbies.
/// </summary>
public class SteamAccountPool
{
    private readonly List<SteamAccount> accounts = [];
    private readonly Lock lockObject = new();
    private readonly DiscordClient discordClient;
    private readonly ILogger<SteamAccountPool> logger;

    public SteamAccountPool(DiscordClient discordClient, ILogger<SteamAccountPool> logger)
    {
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(logger);

        this.discordClient = discordClient;
        this.logger = logger;
    }

    public async Task RegisterAccountAsync(string username, string password)
    {
        string message;
        lock (lockObject)
        {
            if (accounts.Any(a => a.Username == username))
            {
                var existing = accounts.First(a => a.Username == username);
                message = $"Account {username} already exists in pool with state {existing.State}";
            }
            else
            {
                accounts.Add(new SteamAccount
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = username,
                    Password = password,
                    State = AccountState.Ready,
                    RegisteredAt = DateTime.UtcNow
                });
                message = $"Account {username} registered successfully.";
            }
        }

        logger.LogInformation("{Message}", message);
        await discordClient.SendMessageAsync(message);
    }

    public async Task<SteamAccount?> GetAvailableAccountAsync()
    {
        SteamAccount? available;
        string message;

        lock (lockObject)
        {
            available = accounts.FirstOrDefault(a => a.State == AccountState.Ready);
            if (available != null)
            {
                available.State = AccountState.InUse;
                message = $"Account {available.Username} is now in use.";
            }
            else
            {
                message = "No available accounts in pool";
            }
        }

        logger.LogInformation("{Message}", message);
        await discordClient.SendMessageAsync(message);
        return available;
    }

    public SteamAccount? GetAccountByUsername(string username)
    {
        lock (lockObject)
        {
            return accounts.FirstOrDefault(a => a.Username == username);
        }
    }

    public SteamAccount? GetAccountById(string accountId)
    {
        lock (lockObject)
        {
            return accounts.FirstOrDefault(a => a.Id == accountId);
        }
    }

    public async Task ReleaseAccountAsync(string accountId)
    {
        SteamAccount? account;
        lock (lockObject)
        {
            account = accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                account.State = AccountState.Ready;
                account.LastUsedAt = DateTime.UtcNow;
                account.CurrentLobbyId = null;
            }
        }

        if (account != null)
        {
            var message = $"Account {account.Username} released back to pool";
            logger.LogInformation("{Message}", message);
            await discordClient.SendMessageAsync(message);
        }
    }

    public async Task MarkAccountInUseAsync(string accountId, string lobbyId)
    {
        SteamAccount? account;
        lock (lockObject)
        {
            account = accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                account.State = AccountState.InUse;
                account.CurrentLobbyId = lobbyId;
                account.LastUsedAt = DateTime.UtcNow;
            }
        }

        if (account != null)
        {
            var message = $"Account {account.Username} marked in use for lobby {lobbyId}";
            logger.LogInformation("{Message}", message);
            await discordClient.SendMessageAsync(message);
        }
    }

    public AccountPoolStats GetStats()
    {
        lock (lockObject)
        {
            return new AccountPoolStats
            {
                TotalAccounts = accounts.Count,
                ReadyAccounts = accounts.Count(a => a.State == AccountState.Ready),
                InUseAccounts = accounts.Count(a => a.State == AccountState.InUse),
                OnCooldownAccounts = accounts.Count(a => a.State == AccountState.Cooldown),
                Accounts = accounts.ToList()
            };
        }
    }

    public bool HasAvailableAccounts()
    {
        lock (lockObject)
        {
            return accounts.Any(a => a.State == AccountState.Ready);
        }
    }

    public int GetAvailableAccountCount()
    {
        lock (lockObject)
        {
            return accounts.Count(a => a.State == AccountState.Ready);
        }
    }

    public List<SteamAccount> GetAllAccounts()
    {
        lock (lockObject)
        {
            return accounts.ToList();
        }
    }
}
