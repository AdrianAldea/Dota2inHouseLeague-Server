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
    private readonly IMatchRepository matchRepository;
    private readonly IChallengeRepository challengeRepository;
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
        IMatchRepository matchRepository,
        IChallengeRepository challengeRepository,
        IOptions<SteamOptions> steamOptions,
        ILogger<Dota2Service> logger)
    {
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(poolManager);
        ArgumentNullException.ThrowIfNull(playerRepository);
        ArgumentNullException.ThrowIfNull(matchRepository);
        ArgumentNullException.ThrowIfNull(challengeRepository);
        ArgumentNullException.ThrowIfNull(steamOptions);
        ArgumentNullException.ThrowIfNull(logger);

        this.discordClient = discordClient;
        this.poolManager = poolManager;
        this.playerRepository = playerRepository;
        this.matchRepository = matchRepository;
        this.challengeRepository = challengeRepository;
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

    public Task<QueueOperationResponse> MarkPlayerAsReadyAsync(ulong id) => poolManager.MarkPlayerReadyAsync(id);

    public Task<QueueOperationResponse> MarkPlayerAsUnreadyAsync(ulong id) => poolManager.MarkPlayerUnreadyAsync(id);

    public async Task<QueueOperationResponse> JoinQueueAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var player = await playerRepository.GetByUserIdAsync(userId, cancellationToken);
        if (player == null)
        {
            return new QueueOperationResponse
            {
                Success = false,
                Message = "You are not registered. Use `!register <steamId>` first."
            };
        }

        if (player.IsBanned)
        {
            var reason = string.IsNullOrEmpty(player.BanReason) ? "" : $" Reason: {player.BanReason}";
            return new QueueOperationResponse
            {
                Success = false,
                Message = $"You are banned from queuing.{reason}"
            };
        }

        return await poolManager.JoinQueueAsync(player.UserId, player.Username, player.SteamId);
    }

    public Task<QueueOperationResponse> LeaveQueueAsync(ulong userId) => poolManager.LeaveQueueAsync(userId);

    public QueueStatusResponse GetQueueStatus() => poolManager.GetQueueStatus();

    public async Task<QueueOperationResponse> UnregisterPlayerAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var deleted = await playerRepository.DeleteAsync(userId, cancellationToken);
        if (!deleted)
        {
            return new QueueOperationResponse
            {
                Success = false,
                Message = "You are not registered."
            };
        }

        poolManager.RemovePlayerCompletely(userId);

        var message = "You have been unregistered from the league.";
        logger.LogInformation("Player {UserId} unregistered", userId);
        await discordClient.SendMessageAsync($"Player <@{userId}> left the league.");

        return new QueueOperationResponse { Success = true, Message = message };
    }

    public Task<PlayerStatsResponse?> GetPlayerStatsAsync(ulong userId, CancellationToken cancellationToken = default) =>
        playerRepository.GetStatsAsync(userId, cancellationToken);

    public Task<LeaderboardResponse> GetLeaderboardAsync(int topN, CancellationToken cancellationToken = default) =>
        playerRepository.GetLeaderboardAsync(topN, cancellationToken);

    public Task<MatchSummary?> GetCurrentMatchAsync(ulong userId, CancellationToken cancellationToken = default) =>
        matchRepository.GetCurrentMatchForPlayerAsync(userId, cancellationToken);

    public Task<MatchSummary?> GetMatchByIdAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        matchRepository.GetByIdAsync(matchId, cancellationToken);

    public Task<RankResponse?> GetPlayerRankAsync(ulong userId, CancellationToken cancellationToken = default) =>
        playerRepository.GetRankAsync(userId, cancellationToken);

    public Task<RecentMatchesResponse> GetRecentMatchesAsync(ulong userId, int top, CancellationToken cancellationToken = default) =>
        matchRepository.GetRecentMatchesForPlayerAsync(userId, top, cancellationToken);

    public async Task<QueueOperationResponse> ReportMatchAsync(ulong reporterUserId, MatchTeam winningTeam, CancellationToken cancellationToken = default)
    {
        var result = await matchRepository.ReportMatchAsync(reporterUserId, winningTeam, cancellationToken);
        if (result.Success && result.Match != null)
        {
            await discordClient.SendMessageAsync(result.Message);
        }
        return new QueueOperationResponse { Success = result.Success, Message = result.Message };
    }

    public async Task<QueueOperationResponse> LinkSteamIdAsync(ulong userId, ulong steamId, CancellationToken cancellationToken = default)
    {
        var steamIdString = steamId.ToString();

        var updated = await playerRepository.UpdateSteamIdAsync(userId, steamIdString, cancellationToken);
        if (updated == null)
        {
            return new QueueOperationResponse
            {
                Success = false,
                Message = "You are not registered. Use `!register <steamId>` first."
            };
        }

        poolManager.UpdatePlayerSteamId(userId, steamIdString);

        var message = $"Steam ID updated to {steamIdString}.";
        logger.LogInformation("Player {UserId} linked SteamId {SteamId}", userId, steamIdString);

        return new QueueOperationResponse { Success = true, Message = message };
    }

    public async Task<ChallengeOperationResponse> ChallengePlayerAsync(ulong challengerUserId, ulong challengedUserId, CancellationToken cancellationToken = default)
    {
        if (challengerUserId == challengedUserId)
        {
            return new ChallengeOperationResponse { Success = false, Message = "You can't challenge yourself." };
        }

        var challenger = await playerRepository.GetByUserIdAsync(challengerUserId, cancellationToken);
        if (challenger == null)
        {
            return new ChallengeOperationResponse { Success = false, Message = "You are not registered. Use `!register <steamId>` first." };
        }

        var challenged = await playerRepository.GetByUserIdAsync(challengedUserId, cancellationToken);
        if (challenged == null)
        {
            return new ChallengeOperationResponse { Success = false, Message = "Opponent is not registered." };
        }

        var challenge = await challengeRepository.CreateAsync(challengerUserId, challengedUserId, LeagueConstants.ChallengeTtl, cancellationToken);
        var response = await ToResponseAsync(challenge, challenger.Username, challenged.Username);

        var message = $"<@{challengerUserId}> challenges <@{challengedUserId}> to a 1v1! Reply with `!accept_match` or `!decline_match` within {(int)LeagueConstants.ChallengeTtl.TotalMinutes} min.";
        await discordClient.SendMessageAsync(message);

        return new ChallengeOperationResponse
        {
            Success = true,
            Message = $"Challenge sent to {challenged.Username}. Expires at {challenge.ExpiresAt:HH:mm} UTC.",
            Challenge = response
        };
    }

    public async Task<ChallengeOperationResponse> AcceptChallengeAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var challenge = await challengeRepository.AcceptLatestAsync(userId, cancellationToken);
        if (challenge == null)
        {
            return new ChallengeOperationResponse { Success = false, Message = "No pending challenge for you." };
        }

        // Create a 1v1 match: challenger = Radiant, challenged = Dire
        var match = await matchRepository.CreateMatchAsync(
            lobbyId: null,
            players: new[]
            {
                (challenge.ChallengerUserId, MatchTeam.Radiant),
                (challenge.ChallengedUserId, MatchTeam.Dire)
            },
            cancellationToken);

        await challengeRepository.SetResultingMatchAsync(challenge.Id, match.Id, cancellationToken);

        var summary = await matchRepository.GetCurrentMatchForPlayerAsync(userId, cancellationToken);

        var message = $"<@{challenge.ChallengedUserId}> accepted <@{challenge.ChallengerUserId}>'s challenge. Match {match.Id} is in progress.";
        await discordClient.SendMessageAsync(message);

        return new ChallengeOperationResponse
        {
            Success = true,
            Message = $"Challenge accepted. Match {match.Id} created.",
            Match = summary
        };
    }

    public async Task<ChallengeOperationResponse> DeclineChallengeAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var challenge = await challengeRepository.DeclineLatestAsync(userId, cancellationToken);
        if (challenge == null)
        {
            return new ChallengeOperationResponse { Success = false, Message = "No pending challenge for you." };
        }

        var message = $"<@{challenge.ChallengedUserId}> declined <@{challenge.ChallengerUserId}>'s challenge.";
        await discordClient.SendMessageAsync(message);

        return new ChallengeOperationResponse
        {
            Success = true,
            Message = "Challenge declined."
        };
    }

    // ==================== Admin operations ====================

    public async Task<QueueOperationResponse> BanPlayerAsync(ulong userId, string? reason, CancellationToken cancellationToken = default)
    {
        var updated = await playerRepository.SetBanAsync(userId, banned: true, reason, cancellationToken);
        if (updated == null)
        {
            return new QueueOperationResponse { Success = false, Message = "Player is not registered." };
        }

        // also remove from queue if they're in it
        poolManager.RemovePlayerCompletely(userId);

        var msg = $"{updated.Username} has been banned." + (string.IsNullOrEmpty(reason) ? "" : $" Reason: {reason}");
        logger.LogInformation("Player {UserId} banned. Reason={Reason}", userId, reason);
        await discordClient.SendMessageAsync($"<@{userId}> has been banned." + (string.IsNullOrEmpty(reason) ? "" : $" Reason: {reason}"));
        return new QueueOperationResponse { Success = true, Message = msg };
    }

    public async Task<QueueOperationResponse> UnbanPlayerAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var updated = await playerRepository.SetBanAsync(userId, banned: false, reason: null, cancellationToken);
        if (updated == null)
        {
            return new QueueOperationResponse { Success = false, Message = "Player is not registered." };
        }

        var msg = $"{updated.Username} has been unbanned.";
        logger.LogInformation("Player {UserId} unbanned", userId);
        await discordClient.SendMessageAsync($"<@{userId}> has been unbanned.");
        return new QueueOperationResponse { Success = true, Message = msg };
    }

    public Task<QueueOperationResponse> KickPlayerAsync(ulong userId) => poolManager.KickFromLobbyAsync(userId);

    public Task<QueueOperationResponse> ForceStartLobbyAsync() => poolManager.ForceLobbyReadyAsync();

    public Task<QueueOperationResponse> ResetQueueAsync() => poolManager.ResetCurrentLobbyAsync();

    public Task<QueueOperationResponse> AddSteamAccountAsync(string username, string password) =>
        poolManager.AddSteamAccountAsync(username, password);

    public PoolStatusResponse GetPoolStatus() => poolManager.GetPoolStatusResponse();

    private async Task<ChallengeResponse> ToResponseAsync(ChallengeEntity challenge, string? challengerUsername = null, string? challengedUsername = null)
    {
        if (challengerUsername == null)
        {
            var c = await playerRepository.GetByUserIdAsync(challenge.ChallengerUserId);
            challengerUsername = c?.Username ?? challenge.ChallengerUserId.ToString();
        }
        if (challengedUsername == null)
        {
            var c = await playerRepository.GetByUserIdAsync(challenge.ChallengedUserId);
            challengedUsername = c?.Username ?? challenge.ChallengedUserId.ToString();
        }

        return new ChallengeResponse
        {
            Id = challenge.Id,
            ChallengerUserId = challenge.ChallengerUserId,
            ChallengerUsername = challengerUsername,
            ChallengedUserId = challenge.ChallengedUserId,
            ChallengedUsername = challengedUsername,
            CreatedAt = challenge.CreatedAt,
            ExpiresAt = challenge.ExpiresAt,
            Status = challenge.Status,
            ResultingMatchId = challenge.ResultingMatchId
        };
    }
}
