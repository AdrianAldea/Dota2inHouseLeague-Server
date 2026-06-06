using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Configuration;
using Microsoft.Extensions.Options;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.Dota.Internal;
using SteamKit2.Internal;

namespace IHLDota2Bot.Services;

/// <summary>
/// Owns the single SteamKit2 connection to Steam. Hosts the callback loop
/// and exposes the Dota 2 GC operations (create lobby, invite players) so
/// that both <see cref="Dota2Service"/> and the <see cref="LobbyPoolManager"/>
/// can drive Steam-side actions without a DI cycle.
/// </summary>
public sealed class SteamGcClient
{
    private const uint DotaAppId = 570;

    private readonly DiscordClient discordClient;
    private readonly ILogger<SteamGcClient> logger;
    private readonly SteamOptions steamOptions;

    private readonly SteamKit2.SteamClient steamClient;
    private readonly SteamUser steamUser;
    private readonly SteamFriends steamFriends;
    private readonly SteamGameCoordinator steamGameCoordinator;
    private readonly CallbackManager callbackManager;

    private bool isRunning;

    public SteamGcClient(
        DiscordClient discordClient,
        IOptions<SteamOptions> steamOptions,
        ILogger<SteamGcClient> logger)
    {
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(steamOptions);
        ArgumentNullException.ThrowIfNull(logger);

        this.discordClient = discordClient;
        this.steamOptions = steamOptions.Value;
        this.logger = logger;

        steamClient = new SteamKit2.SteamClient();
        steamUser = steamClient.GetHandler<SteamUser>()!;
        steamFriends = steamClient.GetHandler<SteamFriends>()!;
        steamGameCoordinator = steamClient.GetHandler<SteamGameCoordinator>()!;

        callbackManager = new CallbackManager(steamClient);
        callbackManager.Subscribe<SteamKit2.SteamClient.ConnectedCallback>(OnConnected);
        callbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        callbackManager.Subscribe<SteamGameCoordinator.MessageCallback>(OnGCMessage);
    }

    public bool IsRunning => isRunning;

    public Task<bool> StartAsync()
    {
        if (isRunning) return Task.FromResult(true);

        isRunning = true;
        _ = Task.Run(ConnectAndPumpCallbacks);
        return Task.FromResult(true);
    }

    private void ConnectAndPumpCallbacks()
    {
        steamClient.Connect();
        while (isRunning)
        {
            callbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
        }
    }

    public async Task CreateLobbyAsync(CreateLobbyRequest req)
    {
        logger.LogInformation("Sending CMsgPracticeLobbyCreate for lobby {LobbyName}", req.LobbyName);

        var createLobbyMsg = new ClientGCMsgProtobuf<CMsgPracticeLobbyCreate>((uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyCreate);
        createLobbyMsg.Body.lobby_details = new CMsgPracticeLobbySetDetails
        {
            game_name = req.LobbyName,
            pass_key = req.LobbyPassword,
            game_mode = (uint)DOTA_GameMode.DOTA_GAMEMODE_AP,
            server_region = 2
        };

        steamGameCoordinator.Send(createLobbyMsg, DotaAppId);

        var message = $"Lobby created: {req.LobbyName}";
        logger.LogInformation("{Message}", message);
        await discordClient.SendMessageAsync(message);
    }

    public void InvitePlayersToLobby(IList<ulong> steamIds)
    {
        foreach (var steamId in steamIds)
        {
            var inviteMsg = new ClientGCMsgProtobuf<CMsgInviteToLobby>((uint)EGCBaseMsg.k_EMsgGCInviteToLobby);
            inviteMsg.Body.steam_id = steamId;

            logger.LogInformation("Invite sent to SteamID: {SteamId}", steamId);
            steamGameCoordinator.Send(inviteMsg, DotaAppId);
        }
    }

    private void OnGCMessage(SteamGameCoordinator.MessageCallback callback)
    {
        logger.LogInformation("GC message received. AppID: {AppId} Message: {EMsg}", callback.AppID, callback.EMsg);
    }

    private void OnConnected(SteamKit2.SteamClient.ConnectedCallback callback)
    {
        var bot = steamOptions.BotAccount;
        if (string.IsNullOrEmpty(bot.Username) || string.IsNullOrEmpty(bot.Password))
        {
            logger.LogError("Steam bot credentials are not configured. Set Steam:BotAccount in user-secrets.");
            return;
        }

        logger.LogInformation("Attempting Steam authentication as {Username}", bot.Username);
        steamUser.LogOn(new SteamUser.LogOnDetails
        {
            Username = bot.Username,
            Password = bot.Password
        });
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result != EResult.OK)
        {
            logger.LogError("Steam authentication failed: {Result}", callback.Result);
            return;
        }

        logger.LogInformation("Steam authentication successful");
        steamFriends.SetPersonaState(EPersonaState.Online);

        var hello = new ClientGCMsgProtobuf<SteamKit2.GC.Dota.Internal.CMsgClientHello>((uint)EGCBaseClientMsg.k_EMsgGCClientHello);
        steamGameCoordinator.Send(hello, DotaAppId);

        var gamePlayed = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
        gamePlayed.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed { game_id = DotaAppId });
        steamClient.Send(gamePlayed);

        logger.LogInformation("Notified Steam that we are 'playing' Dota 2.");
    }
}
