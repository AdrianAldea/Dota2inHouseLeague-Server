using IHLDota2Bot.Clients;
using IHLDota2Bot.Configuration;
using Microsoft.Extensions.Options;

namespace IHLDota2Bot.Services;

/// <summary>
/// Registry of <see cref="SteamGcClient"/> instances keyed by Steam username.
/// At startup, holds one client per configured Steam account (Steam:BotAccount
/// plus everything in Steam:PoolAccounts). At runtime, admins can register new
/// accounts via <see cref="TryAddAccountAsync"/> so an `!add_account` flow can
/// reach a real Steam connection — not just bookkeeping.
/// </summary>
public sealed class SteamGcClientPool : IHostedService
{
    private readonly Dictionary<string, ISteamGcClient> clientsByUsername;
    private readonly Lock lockObject = new();
    private readonly DiscordClient discordClient;
    private readonly ILoggerFactory loggerFactory;
    private ISteamGcClient? defaultClient;
    private readonly ILogger<SteamGcClientPool> logger;

    public SteamGcClientPool(
        IOptions<SteamOptions> steamOptions,
        DiscordClient discordClient,
        ILoggerFactory loggerFactory,
        ILogger<SteamGcClientPool> logger)
    {
        ArgumentNullException.ThrowIfNull(steamOptions);
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(logger);

        this.discordClient = discordClient;
        this.loggerFactory = loggerFactory;
        this.logger = logger;
        clientsByUsername = new Dictionary<string, ISteamGcClient>(StringComparer.OrdinalIgnoreCase);

        var options = steamOptions.Value;

        // BotAccount comes first so it becomes the default — used by the admin
        // /create passthrough and as a sensible "first" choice for ad-hoc ops.
        var bot = options.BotAccount;
        if (HasCredentials(bot))
        {
            var client = BuildClient(bot.Username, bot.Password);
            clientsByUsername[bot.Username] = client;
            defaultClient = client;
        }
        else
        {
            logger.LogWarning("Steam:BotAccount is not configured. The /create endpoint and any ad-hoc Steam operation will be disabled.");
        }

        foreach (var account in options.PoolAccounts)
        {
            if (!HasCredentials(account))
            {
                logger.LogWarning("Skipping pool account with missing credentials");
                continue;
            }
            if (clientsByUsername.ContainsKey(account.Username))
            {
                // Already registered as BotAccount; deduplicate so we only run one SteamClient per username.
                continue;
            }
            var client = BuildClient(account.Username, account.Password);
            clientsByUsername[account.Username] = client;
            defaultClient ??= client;
        }

        logger.LogInformation("SteamGcClientPool initialised with {Count} client(s): {Usernames}",
            clientsByUsername.Count,
            string.Join(", ", clientsByUsername.Keys));
    }

    /// <summary>The fallback client — first configured BotAccount, or first PoolAccount if no BotAccount.</summary>
    public ISteamGcClient? Default
    {
        get { lock (lockObject) { return defaultClient; } }
    }

    public IReadOnlyDictionary<string, ISteamGcClient> All
    {
        get
        {
            lock (lockObject)
            {
                // Snapshot copy so callers iterating the dictionary aren't surprised by
                // a concurrent TryAddAccountAsync mutation.
                return clientsByUsername.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public ISteamGcClient? GetByUsername(string username)
    {
        lock (lockObject)
        {
            return clientsByUsername.GetValueOrDefault(username);
        }
    }

    /// <summary>
    /// Registers a Steam account at runtime and starts its connection. Returns the
    /// usual operation envelope; false if the username is already registered or the
    /// credentials are missing. Used by the admin `!add_account` / POST /admin/add_account
    /// flow so accounts added after boot become full Steam connections, not just
    /// bookkeeping entries.
    /// </summary>
    public Task<(bool Added, string Message)> TryAddAccountAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult((false, "Username and password are required."));
        }

        ISteamGcClient client;
        lock (lockObject)
        {
            if (clientsByUsername.ContainsKey(username))
            {
                return Task.FromResult((false, $"Steam client for `{username}` already exists."));
            }

            client = BuildClient(username, password);
            clientsByUsername[username] = client;
            defaultClient ??= client;
        }

        logger.LogInformation("SteamGcClientPool: registered new account {Username}; starting connection", username);
        // Fire-and-forget start; the connection task pumps callbacks until StopAsync.
        _ = client.StartAsync();
        return Task.FromResult((true, $"Steam client `{username}` registered and starting."));
    }

    // ── IHostedService: auto-connect every bot on app boot ──
    //
    // Without this, the SteamGcClient instances were idle until someone
    // explicitly hit POST /api/server/start (the "Start Server" admin button).
    // That meant the !queue/!ready path could fill a lobby with nobody able
    // to actually host it on Steam. Auto-starting at app boot means players
    // can !queue as soon as the API responds. Manual /api/server/start still
    // works as an idempotent retry.

    async Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        var ok = await StartAllAsync();
        if (!ok)
        {
            logger.LogWarning(
                "SteamGcClientPool: at least one bot didn't start cleanly on app boot. " +
                "Check Steam credentials in user-secrets, then POST /api/server/start to retry.");
        }
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken) => StopAllAsync();

    /// <summary>Starts every configured client. Returns true if every one accepted the start request.</summary>
    public async Task<bool> StartAllAsync()
    {
        ISteamGcClient[] snapshot;
        lock (lockObject)
        {
            if (clientsByUsername.Count == 0)
            {
                logger.LogError("StartAllAsync called but no Steam accounts are configured.");
                return false;
            }
            snapshot = clientsByUsername.Values.ToArray();
        }

        var results = await Task.WhenAll(snapshot.Select(c => c.StartAsync()));
        return results.All(ok => ok);
    }

    public async Task StopAllAsync()
    {
        ISteamGcClient[] snapshot;
        lock (lockObject)
        {
            snapshot = clientsByUsername.Values.ToArray();
        }
        await Task.WhenAll(snapshot.Select(c => c.StopAsync()));
    }

    /// <summary>
    /// Synchronously tells every bot to leave its hosted lobby. Hooked into
    /// AppDomain.ProcessExit and UnhandledException so a crash or hard
    /// shutdown still gives the Dota GC a leave signal before we go away —
    /// IHostedService.StopAsync only runs on graceful shutdown.
    /// </summary>
    public void DestroyAllActiveLobbies()
    {
        ISteamGcClient[] snapshot;
        lock (lockObject)
        {
            snapshot = clientsByUsername.Values.ToArray();
        }

        foreach (var client in snapshot)
        {
            try
            {
                client.DestroyActiveLobby();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "DestroyActiveLobby threw for {Username}", client.Username);
            }
        }
    }

    private static bool HasCredentials(SteamAccountCredentials credentials) =>
        !string.IsNullOrWhiteSpace(credentials?.Username) && !string.IsNullOrWhiteSpace(credentials.Password);

    private ISteamGcClient BuildClient(string username, string password)
    {
        // Use a per-account logger category so multi-bot log lines stay distinguishable
        // in the Aspire dashboard without having to read every message body.
        var perAccountLogger = loggerFactory.CreateLogger($"IHLDota2Bot.Services.SteamGcClient[{username}]");
        return new SteamGcClient(username, password, discordClient, perAccountLogger);
    }
}
