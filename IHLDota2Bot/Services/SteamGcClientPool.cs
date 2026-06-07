using IHLDota2Bot.Clients;
using IHLDota2Bot.Configuration;
using Microsoft.Extensions.Options;

namespace IHLDota2Bot.Services;

/// <summary>
/// Registry of <see cref="SteamGcClient"/> instances keyed by Steam username.
/// Holds one client per configured Steam account (the singular Steam:BotAccount
/// plus everything in Steam:PoolAccounts), so league lobbies can run on
/// distinct Steam accounts concurrently.
/// </summary>
public sealed class SteamGcClientPool
{
    private readonly Dictionary<string, SteamGcClient> clientsByUsername;
    private readonly SteamGcClient? defaultClient;
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

        this.logger = logger;
        clientsByUsername = new Dictionary<string, SteamGcClient>(StringComparer.OrdinalIgnoreCase);

        var options = steamOptions.Value;

        // BotAccount comes first so it becomes the default — used by the admin
        // /create passthrough and as a sensible "first" choice for ad-hoc ops.
        var bot = options.BotAccount;
        if (HasCredentials(bot))
        {
            var client = BuildClient(bot.Username, bot.Password, discordClient, loggerFactory);
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
            var client = BuildClient(account.Username, account.Password, discordClient, loggerFactory);
            clientsByUsername[account.Username] = client;
            defaultClient ??= client;
        }

        logger.LogInformation("SteamGcClientPool initialised with {Count} client(s): {Usernames}",
            clientsByUsername.Count,
            string.Join(", ", clientsByUsername.Keys));
    }

    /// <summary>The fallback client — first configured BotAccount, or first PoolAccount if no BotAccount.</summary>
    public SteamGcClient? Default => defaultClient;

    public IReadOnlyDictionary<string, SteamGcClient> All => clientsByUsername;

    public SteamGcClient? GetByUsername(string username) =>
        clientsByUsername.GetValueOrDefault(username);

    /// <summary>Starts every configured client. Returns true if every one accepted the start request.</summary>
    public async Task<bool> StartAllAsync()
    {
        if (clientsByUsername.Count == 0)
        {
            logger.LogError("StartAllAsync called but no Steam accounts are configured.");
            return false;
        }

        var results = await Task.WhenAll(clientsByUsername.Values.Select(c => c.StartAsync()));
        return results.All(ok => ok);
    }

    public async Task StopAllAsync()
    {
        await Task.WhenAll(clientsByUsername.Values.Select(c => c.StopAsync()));
    }

    private static bool HasCredentials(SteamAccountCredentials credentials) =>
        !string.IsNullOrWhiteSpace(credentials?.Username) && !string.IsNullOrWhiteSpace(credentials.Password);

    private static SteamGcClient BuildClient(string username, string password, DiscordClient discordClient, ILoggerFactory factory)
    {
        // Use a per-account logger category so multi-bot log lines stay distinguishable
        // in the Aspire dashboard without having to read every message body.
        var perAccountLogger = factory.CreateLogger($"IHLDota2Bot.Services.SteamGcClient[{username}]");
        return new SteamGcClient(username, password, discordClient, perAccountLogger);
    }
}
