using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.Requests;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.Dota.Internal;
using SteamKit2.Internal;

namespace IHLDota2Bot.Services;

/// <summary>
/// One SteamKit2 connection for a single Steam account. Owns the callback
/// loop and exposes the Dota 2 GC operations (create lobby, invite players).
///
/// Instances are created and owned by <see cref="SteamGcClientPool"/>, which
/// holds one client per configured bot/pool account so the league can run
/// concurrent Dota lobbies.
/// </summary>
public sealed class SteamGcClient
{
    private const uint DotaAppId = 570;

    private readonly string username;
    private readonly string password;
    private readonly DiscordClient discordClient;
    private readonly ILogger logger;

    private readonly SteamKit2.SteamClient steamClient;
    private readonly SteamUser steamUser;
    private readonly SteamFriends steamFriends;
    private readonly SteamGameCoordinator steamGameCoordinator;
    private readonly CallbackManager callbackManager;

    private bool isRunning;
    private bool isLoggedOn;

    public SteamGcClient(string username, string password, DiscordClient discordClient, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(logger);

        this.username = username;
        this.password = password;
        this.discordClient = discordClient;
        this.logger = logger;

        steamClient = new SteamKit2.SteamClient();
        steamUser = steamClient.GetHandler<SteamUser>()!;
        steamFriends = steamClient.GetHandler<SteamFriends>()!;
        steamGameCoordinator = steamClient.GetHandler<SteamGameCoordinator>()!;

        callbackManager = new CallbackManager(steamClient);
        callbackManager.Subscribe<SteamKit2.SteamClient.ConnectedCallback>(OnConnected);
        callbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        callbackManager.Subscribe<SteamGameCoordinator.MessageCallback>(OnGCMessage);

        StartAsync();
    }

    public string Username => username;
    public bool IsRunning => isRunning;
    public bool IsLoggedOn => isLoggedOn;

    public Task<bool> StartAsync()
    {
        if (isRunning) return Task.FromResult(true);

        isRunning = true;
        _ = Task.Run(ConnectAndPumpCallbacks);
        return Task.FromResult(true);
    }

    public Task StopAsync()
    {
        isRunning = false;
        isLoggedOn = false;
        steamClient.Disconnect();
        return Task.CompletedTask;
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
        logger.LogInformation("[{Username}] Sending CMsgPracticeLobbyCreate for lobby {LobbyName}", username, req.LobbyName);

        var createLobbyMsg = new ClientGCMsgProtobuf<CMsgPracticeLobbyCreate>((uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyCreate);
        createLobbyMsg.Body.lobby_details = new CMsgPracticeLobbySetDetails
        {
            game_name = req.LobbyName,
            pass_key = req.LobbyPassword,
            game_mode = (uint)DOTA_GameMode.DOTA_GAMEMODE_AP,
            server_region = 2
        };

        steamGameCoordinator.Send(createLobbyMsg, DotaAppId);

        var message = $"[{username}] Lobby created: {req.LobbyName}";
        logger.LogInformation("{Message}", message);
        await discordClient.SendMessageAsync(message);
    }

    public void InvitePlayersToLobby(IList<ulong> steamIds)
    {
        foreach (var steamId in steamIds)
        {
            var inviteMsg = new ClientGCMsgProtobuf<CMsgInviteToLobby>((uint)EGCBaseMsg.k_EMsgGCInviteToLobby);
            inviteMsg.Body.steam_id = steamId;

            logger.LogInformation("[{Username}] Invite sent to SteamID {SteamId}", username, steamId);
            steamGameCoordinator.Send(inviteMsg, DotaAppId);
        }
    }

    private void OnGCMessage(SteamGameCoordinator.MessageCallback callback)
    {
        logger.LogInformation("[{Username}] GC message: AppID {AppId} EMsg {EMsg}", username, callback.AppID, callback.EMsg);
    }

    private void OnConnected(SteamKit2.SteamClient.ConnectedCallback callback)
    {
        logger.LogInformation("[{Username}] Attempting Steam authentication", username);
        steamUser.LogOn(new SteamUser.LogOnDetails
        {
            Username = username,
            Password = password
        });
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result != EResult.OK)
        {
            logger.LogError("[{Username}] Steam authentication failed: {Result}", username, callback.Result);
            return;
        }

        isLoggedOn = true;
        logger.LogInformation("[{Username}] Steam authentication successful", username);
        steamFriends.SetPersonaState(EPersonaState.Online);

        var hello = new ClientGCMsgProtobuf<SteamKit2.GC.Dota.Internal.CMsgClientHello>((uint)EGCBaseClientMsg.k_EMsgGCClientHello);
        steamGameCoordinator.Send(hello, DotaAppId);

        var gamePlayed = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
        gamePlayed.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed { game_id = DotaAppId });
        steamClient.Send(gamePlayed);

        logger.LogInformation("[{Username}] Notified Steam that we are 'playing' Dota 2", username);
    }
}
