using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Endpoints.GameManager;
using IHLDota2Bot.Endpoints.StartUp;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.Dota.Internal;
using SteamKit2.Internal;

namespace IHLDota2Bot.Services;

public class Dota2Service
{
    private bool isRunning;
    private const uint DOTA_APP_ID = 570;
    private readonly List<Player> playerList = [];
    private readonly DiscordClient discordClient;
    private readonly SteamClient steamClient;
    private readonly SteamFriends steamFriends;
    private readonly SteamGameCoordinator steamGameCoordinator;
    private readonly SteamUser steamUser;
    private readonly CallbackManager callbackManager;
    private readonly LobbyPoolManager poolManager;

    public Dota2Service(DiscordClient discordClient)
    {
        discordClient = discordClient ?? throw new ArgumentNullException(nameof(discordClient));

        poolManager = new LobbyPoolManager(discordClient);
        steamClient = new SteamClient();

        steamUser = steamClient.GetHandler<SteamUser>()!;
        steamFriends = steamClient.GetHandler<SteamFriends>()!;
        steamGameCoordinator = steamClient.GetHandler<SteamGameCoordinator>()!;

        callbackManager = new CallbackManager(steamClient);
        callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        callbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        callbackManager.Subscribe<SteamGameCoordinator.MessageCallback>(OnGCMessage);
    }

    private async Task RegisterMatchResultsAsync()
    {
        // unqueue lobby from pool, mark it as ended, and update player stats in the database based on the match results
        // poolManager.MarkLobbyAsFinished(lobbyId, winningTeam);
    }

    private async Task CreateLobbyAsync(CreateLobbyRequest request)
    {
        // Get free boot steam account from the pool
        // poolManager.GetAvailableSteamAccount(); and connect using that account to Steam (similar to StartServer but with specific credentials)
        // create lobby using that account and send the lobby details back to the Discord channel
        //poolManager._lobbyQueue.Enqueue(new LobbyInfo { LobbyName = "My Lobby", LobbyPassword = "defaultpassword" });

        CreateLobby(request);
    }

    public async Task<bool> StartServer(StartUpRequest request)
    {
        isRunning = true;

        try
        {
            _ = Task.Run(() => ConnectToSteam());
        }
        catch (Exception ex)
        {
            throw new Exception($"{nameof(StartServer)}: Failed to start server: {ex.Message}", ex);
        }

        return await Task.FromResult(true);
    }

    private void ConnectToSteam()
    {
        isRunning = true;

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

            Console.WriteLine($"{nameof(InvitePlayersToLobby)} Invite send to SteamID: {steamId}");
            steamGameCoordinator.Send(inviteMsg, DOTA_APP_ID);
        }
    }

    private void OnGCMessage(SteamGameCoordinator.MessageCallback callback)
    {
        Console.WriteLine($"{nameof(OnGCMessage)}: AppID - {callback.AppID} Message: {callback.EMsg}.");
    }

    private void OnConnected(SteamClient.ConnectedCallback callback)
    {
        Console.WriteLine($"{nameof(OnConnected)}: Attempting authentication...");

        var request = new StartUpRequest { Username = "dota2inhouseleague", Password = "Romania90." }; // TODO
        steamUser.LogOn(new SteamUser.LogOnDetails
        {
            Username = request.Username,
            Password = request.Password,
            //AuthCode = authCode, // Se completează dacă este cerut (email)
            //TwoFactorCode = twoFactorAuth, // Se completează dacă este cerut (authenticator app)
            //SentryFileHash = sentryHash, // Pentru autentificarea mașinii
            //ShouldRememberPassword = true,
        });

        Console.WriteLine($"{nameof(OnConnected)}: {request.Username} Connected to Steam!");
    }

    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result == EResult.OK)
        {
            Console.WriteLine($"{nameof(OnLoggedOn)}: Authentication successful !");
            steamFriends.SetPersonaState(EPersonaState.Online);

            var hello = new ClientGCMsgProtobuf<SteamKit2.GC.Dota.Internal.CMsgClientHello>((uint)EGCBaseClientMsg.k_EMsgGCClientHello);
            steamGameCoordinator.Send(hello, 570);

            var gamePlayed = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
            gamePlayed.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
            {
                game_id = 570,
                // game_ip_address = 0,
                // game_port = 0,
            });
            steamClient.Send(gamePlayed);
            Console.WriteLine($"{nameof(OnConnected)}: We notified Steam that we were 'playing' Dota 2.");
        }
    }

    public void CreateLobby(CreateLobbyRequest createLobbyRequest)
    {
        Console.WriteLine($"{nameof(CreateLobby)}: Attempting to send lobby creation message to Dota 2 GC...");

        var createLobbyMsg = new ClientGCMsgProtobuf<CMsgPracticeLobbyCreate>((uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyCreate);

        createLobbyMsg.Body.lobby_details = new CMsgPracticeLobbySetDetails();
        createLobbyMsg.Body.lobby_details.game_name = createLobbyRequest.LobbyName;
        createLobbyMsg.Body.lobby_details.pass_key = createLobbyRequest.LobbyPassword;
        createLobbyMsg.Body.lobby_details.game_mode = (uint)DOTA_GameMode.DOTA_GAMEMODE_AP;
        createLobbyMsg.Body.lobby_details.server_region = 2;

        Console.WriteLine($"{nameof(CreateLobby)}: Sending CMsgPracticeLobbyCreate for lobby: {createLobbyRequest.LobbyName}");
        steamGameCoordinator.Send(createLobbyMsg, DOTA_APP_ID);

        var message = $"{nameof(CreateLobby)}: Lobby created: {createLobbyRequest.LobbyName}";
        Console.WriteLine(message);
        discordClient.SendMessageAsync(message);
    }

    public  async Task<RegisteredPlayer> RegisterPlayer(RegisterPlayerRequest request)
    {
        poolManager.RegisterPlayer(request.UserId, request.Username, request.SteamId.ToString() );
        return new RegisteredPlayer { SteamId = request.SteamId.ToString(), Username = request.Username, UserId = request.UserId.ToString() };
    }

    public async Task<PlayerProfile?> GetPlayerProfile(ulong playerId)
    {
        var player = playerList.FirstOrDefault(p => p.UserId == playerId);
        return player == null
            ? null
            : new PlayerProfile
        {
            Id = player.UserId.ToString(),
            Name = player.Username
        };
    }

    public async Task MarkPlayerAsReady(ulong id)
    {
        poolManager.MarkPlayerReady(id);
    }
}
