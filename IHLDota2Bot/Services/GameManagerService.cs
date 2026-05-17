using Discord;
using Discord.WebSocket;
using IHLDota2Bot.Endpoints.Discord;
using IHLDota2Bot.Endpoints.GameManager;
using IHLDota2Bot.Endpoints.StartUp;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.Dota.Internal;
using SteamKit2.Internal;

namespace IHLDota2Bot.Services;

public class GameManagerService
{
    private bool isRunning;
    private const uint DOTA_APP_ID = 570;
    private const string DiscordToken = "MTM3MzQwMTIxMjkzNzgzMDU3Mg.GwOJf5.ZBOX0Fm6od95W77Ow7XDiZVgJ8a9Bqh4EaFiWE";
    private readonly SteamClient steamClient;
    private readonly DiscordSocketClient discordClient;
    private readonly SteamFriends steamFriends;
    private readonly SteamGameCoordinator steamGameCoordinator;
    private readonly SteamUser steamUser;
    private readonly CallbackManager callbackManager;
    private readonly LobbyPoolManager poolManager;

    public GameManagerService()
    {
        steamClient = new SteamClient();

        steamUser = steamClient.GetHandler<SteamUser>()!;
        steamFriends = steamClient.GetHandler<SteamFriends>()!;
        steamGameCoordinator = steamClient.GetHandler<SteamGameCoordinator>()!;

        callbackManager = new CallbackManager(steamClient);
        callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        callbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        callbackManager.Subscribe<SteamGameCoordinator.MessageCallback>(OnGCMessage);

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        };
        discordClient = new DiscordSocketClient();
        discordClient = new DiscordSocketClient(config);
        discordClient.MessageReceived += HandleMessageAsync;

        poolManager = new LobbyPoolManager();
    }

    private async Task HandleMessageAsync(SocketMessage message)
    {
        if (message.Author.IsBot) return;

        var userMessage = message as SocketUserMessage;
        if (userMessage == null) return;

        var content = userMessage.Content?.Trim();
        if (string.IsNullOrEmpty(content)) return;

        if (!content.StartsWith('!'))
            return;

        var parts = content[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0];

        if (Enum.TryParse<DiscordAction>(command, out DiscordAction action))
        {
            Console.WriteLine($"{nameof(HandleMessageAsync)}: Command - {action}");
        }

        switch (action)
        {
            case DiscordAction.REGISTER:
                await RegisterPlayerAsync(message, parts);
                break;
            case DiscordAction.CREATE_LOBBY:
                await CreateLobbyAsync();
                break;
            case DiscordAction.INVITE_LOBBY:
                await InviteToLobbyAsync();
                break;
            case DiscordAction.PROFILE:
                await ShowProfileAsync(message);
                break;
                //case DiscordAction.MATCH:
                //    await CreateDraftMatchAsync(message);
                //    break;
                //case DiscordAction.PICK:
                //    await HandlePickAsync(message, parts);
                //    break;
                //case DiscordAction.REPORT:
                //    await HandleMatchResultAsync(message, parts);
                //    break;
                //case DiscordAction.LEADERBOARD:
                //    await ShowLeaderboardAsync(message);
                //    break;
                //case DiscordAction.MATCHES:
                //    await ShowRecentMatchesAsync(message);
                //    break;
                //case DiscordAction.MYSTATS:
                //    await ShowMyStatsAsync(message);
                //    break;
                //case DiscordAction.CHALLENGE:
                //    await ChallengePlayerAsync(message);
                //    break;
                //case DiscordAction.ACCEPT_MATCH:
                //    await AcceptChallengeAsync(message);
                //    break;
                //case DiscordAction.DECLINE_MATCH:
                //    await DeclineChallengeAsync(message);
                //    break;
                //case DiscordAction.REPORT_MATCH:
                //    await ReportMatch1v1Async(message, parts);
                //    break;
                //case DiscordAction.COMMANDS:
                //    await ShowCommandsAsync(message);
                //    break;
        }
    }

    private async Task InviteToLobbyAsync()
    {
        InvitePlayersToLobby([]);
    }

    private async Task ShowProfileAsync(SocketMessage message)
    {
        ulong authorId = message.Author.Id;

        // TODO: Fetch player information from the database using the authorId

        var profile = $"**Profile for {"Username"}**\n" +
                        $"- SteamID: {"SteamId"}\n" +
                        $"- Rating: {"Rating"}\n" +
                        $"- Matches: {"Matches"}\n" +
                        $"- Wins: {"Wins"}";

        Console.WriteLine($"{nameof(ShowProfileAsync)}: " + message.Content);
        await message.Channel.SendMessageAsync(profile);
    }

    private async Task RegisterPlayerAsync(SocketMessage message, string[] messageList)
    {
        var steamIdOptional = await GetSteamIdIfValidMessageAsync(message, messageList);
        if (!steamIdOptional.IsSpecified) return;

        var steamId = steamIdOptional.Value;
        var userId = message.Author.Id;

        // TODO: Store this mapping in a database or in-memory store

        Console.WriteLine($"{nameof(RegisterPlayerAsync)}: " + message.Content);
        await message.Channel.SendMessageAsync($"You have successfully registered as {"TODO.PlayerUsername"} (SteamID: {steamId})!");
    }

    private static async Task<Optional<ulong>> GetSteamIdIfValidMessageAsync(SocketMessage message, string[] message_list)
    {
        bool is_valid = message_list.Length >= 2;

        if (!ulong.TryParse(message_list[1], out ulong steamId))
        {
            is_valid = false;
        }
        // Basic SteamId check (doesn't check if it exists, only if it "looks" like one)
        if (!is_valid ||
            steamId < 10000000000000000UL ||    // fewer than 17 digits
            steamId > 99999999999999999UL       // more than 17 digits
        )
        {
            Console.WriteLine($"{nameof(GetSteamIdIfValidMessageAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(DiscordResponse.INVALID_REGISTER_COMMAND.ToString());
            return Optional<ulong>.Unspecified;
        }

        return steamId;
    }

    private async Task CreateLobbyAsync()
    {
        CreateLobby(new CreateLobbyRequest { LobbyName = "My Lobby", LobbyPassword = "defaultpassword" });
    }

    public async Task<bool> StartServer(StartUpRequest request)
    {
        isRunning = true;

        try
        {
            _ = Task.Run(() => ConnectToSteam());
            _ = Task.Run(() => ConnectToDiscord(new()));
        }
        catch (Exception ex)
        {
            throw new Exception($"{nameof(StartServer)}: Failed to start server: {ex.Message}", ex);
        }

        return await Task.FromResult(true);
    }

    public async Task<bool> ConnectToDiscord(DiscordStartUpRequest request)
    {
        try
        {
            await discordClient.LoginAsync(TokenType.Bot, DiscordToken);
            await discordClient.StartAsync();
            Console.WriteLine($"{nameof(ConnectToDiscord)}: Connected to Discord!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{nameof(ConnectToDiscord)}: Failed to connect to Discord: {ex.Message}");
            return false;
        }
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
        createLobbyMsg.Body.lobby_details.game_mode = (uint)DOTA_GameMode.DOTA_GAMEMODE_AP;
        createLobbyMsg.Body.pass_key = createLobbyRequest.LobbyPassword;
        createLobbyMsg.Body.lobby_details.server_region = 2;

        Console.WriteLine($"{nameof(CreateLobby)}: Sending CMsgPracticeLobbyCreate for lobby: {createLobbyRequest.LobbyName}");
        steamGameCoordinator.Send(createLobbyMsg, DOTA_APP_ID);
    }
}
