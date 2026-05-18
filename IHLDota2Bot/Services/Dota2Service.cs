using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Configuration;
using IHLDota2Bot.Data.Entities;
using IHLDota2Bot.Data.Repositories;
using Microsoft.Extensions.Options;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.Dota.Internal;
using SteamKit2.Internal;

namespace IHLDota2Bot.Services;

public class Dota2Service
{
    private const uint DotaAppId = 570;

    private readonly DiscordClient discordClient;
    private readonly LobbyPoolManager poolManager;
    private readonly IPlayerRepository playerRepository;
    private readonly ILogger<Dota2Service> logger;
    private readonly SteamOptions steamOptions;
    private readonly SteamKit2.SteamClient steamClient;
    private readonly SteamUser steamUser;
    private readonly SteamFriends steamFriends;
    private readonly SteamGameCoordinator steamGameCoordinator;
    private readonly CallbackManager callbackManager;
    private bool isRunning;

    public Dota2Service(
        DiscordClient discordClient,
        LobbyPoolManager poolManager,
        IPlayerRepository playerRepository,
        IOptions<SteamOptions> steamOptions,
        ILogger<Dota2Service> logger)
    {
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(poolManager);
        ArgumentNullException.ThrowIfNull(playerRepository);
        ArgumentNullException.ThrowIfNull(steamOptions);
        ArgumentNullException.ThrowIfNull(logger);

        this.discordClient = discordClient;
        this.poolManager = poolManager;
        this.playerRepository = playerRepository;
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

    public Task<bool> StartServerAsync()
    {
        if (isRunning) return Task.FromResult(true);

        isRunning = true;
        _ = Task.Run(ConnectToSteam);
        return Task.FromResult(true);
    }

    private void ConnectToSteam()
    {
        steamClient.Connect();
        while (isRunning)
        {
            callbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
        }
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

    public async Task CreateLobbyAsync(CreateLobbyRequest createLobbyRequest)
    {
        logger.LogInformation("Sending CMsgPracticeLobbyCreate for lobby: {LobbyName}", createLobbyRequest.LobbyName);

        var createLobbyMsg = new ClientGCMsgProtobuf<CMsgPracticeLobbyCreate>((uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyCreate);
        createLobbyMsg.Body.lobby_details = new CMsgPracticeLobbySetDetails
        {
            game_name = createLobbyRequest.LobbyName,
            pass_key = createLobbyRequest.LobbyPassword,
            game_mode = (uint)DOTA_GameMode.DOTA_GAMEMODE_AP,
            server_region = 2
        };

        steamGameCoordinator.Send(createLobbyMsg, DotaAppId);

        var message = $"Lobby created: {createLobbyRequest.LobbyName}";
        logger.LogInformation("{Message}", message);
        await discordClient.SendMessageAsync(message);
    }

    public async Task<RegisteredPlayer> RegisterPlayerAsync(RegisterPlayerRequest request, CancellationToken cancellationToken = default)
    {
        var steamIdString = request.SteamId.ToString();

        var persisted = await playerRepository.UpsertAsync(new PlayerEntity
        {
            UserId = request.UserId,
            Username = request.Username,
            SteamId = steamIdString
        }, cancellationToken);

        await poolManager.RegisterPlayerAsync(request.UserId, request.Username, steamIdString);

        return new RegisteredPlayer
        {
            SteamId = persisted.SteamId,
            Username = persisted.Username,
            UserId = persisted.UserId.ToString()
        };
    }

    public async Task<PlayerProfile?> GetPlayerProfileAsync(ulong playerId, CancellationToken cancellationToken = default)
    {
        var player = await playerRepository.GetByUserIdAsync(playerId, cancellationToken);
        if (player == null) return null;

        return new PlayerProfile
        {
            Id = player.UserId.ToString(),
            Name = player.Username
        };
    }

    public Task MarkPlayerAsReadyAsync(ulong id) => poolManager.MarkPlayerReadyAsync(id);
}
