namespace IHLDota2Bot;

/// <summary>
/// Bookkeeping for the set of Steam accounts the league can hand out to matches.
/// Pure domain — no Discord/UI dependency. Each state transition is logged so
/// the Aspire dashboard shows who has what, without spamming the chat channel.
/// </summary>
public class SteamAccountPool
{
    private readonly List<SteamAccount> accounts = [];
    private readonly Lock lockObject = new();
    private readonly ILogger<SteamAccountPool> logger;

    public SteamAccountPool(ILogger<SteamAccountPool> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        this.logger = logger;
    }

    public Task RegisterAccountAsync(string username, string password)
    {
        lock (lockObject)
        {
            var existing = accounts.FirstOrDefault(a => a.Username == username);
            if (existing != null)
            {
                logger.LogInformation(
                    "Account {Username} already exists in pool (state {State})",
                    username,
                    existing.State
                );
                return Task.CompletedTask;
            }

            accounts.Add(
                new SteamAccount
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = username,
                    Password = password,
                    State = AccountState.Ready,
                    RegisteredAt = DateTime.UtcNow,
                }
            );
            logger.LogInformation("Account {Username} registered in pool", username);
        }
        return Task.CompletedTask;
    }

    public Task<SteamAccount?> GetAvailableAccountAsync()
    {
        SteamAccount? available;
        lock (lockObject)
        {
            available = accounts.FirstOrDefault(a => a.State == AccountState.Ready);
            if (available != null)
            {
                available.State = AccountState.InUse;
                logger.LogInformation(
                    "Account {Username} reserved (now InUse)",
                    available.Username
                );
            }
            else
            {
                logger.LogWarning("No available accounts in pool");
            }
        }
        return Task.FromResult(available);
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

    public Task ReleaseAccountAsync(string accountId)
    {
        lock (lockObject)
        {
            var account = accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                account.State = AccountState.Ready;
                account.LastUsedAt = DateTime.UtcNow;
                account.CurrentLobbyId = null;
                logger.LogInformation("Account {Username} released back to pool", account.Username);
            }
        }
        return Task.CompletedTask;
    }

    public Task MarkAccountInUseAsync(string accountId, string lobbyId)
    {
        lock (lockObject)
        {
            var account = accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                account.State = AccountState.InUse;
                account.CurrentLobbyId = lobbyId;
                account.LastUsedAt = DateTime.UtcNow;
                logger.LogInformation(
                    "Account {Username} marked in use for lobby {LobbyId}",
                    account.Username,
                    lobbyId
                );
            }
        }
        return Task.CompletedTask;
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
                Accounts = accounts.ToList(),
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
