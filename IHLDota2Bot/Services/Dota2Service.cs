using IHLDota2Bot.Clients;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Data.Entities;
using IHLDota2Bot.Data.Repositories;

namespace IHLDota2Bot.Services;

public class Dota2Service
{
    private readonly DiscordClient discordClient;
    private readonly LobbyPoolManager poolManager;
    private readonly SteamGcClientPool steamGcPool;
    private readonly IPlayerRepository playerRepository;
    private readonly IMatchRepository matchRepository;
    private readonly IChallengeRepository challengeRepository;
    private readonly ILogger<Dota2Service> logger;

    private const string NotRegisteredMessage = "You are not registered. Use `!register <steamId>` first.";

    public Dota2Service(
        DiscordClient discordClient,
        LobbyPoolManager poolManager,
        SteamGcClientPool steamGcPool,
        IPlayerRepository playerRepository,
        IMatchRepository matchRepository,
        IChallengeRepository challengeRepository,
        ILogger<Dota2Service> logger)
    {
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(poolManager);
        ArgumentNullException.ThrowIfNull(steamGcPool);
        ArgumentNullException.ThrowIfNull(playerRepository);
        ArgumentNullException.ThrowIfNull(matchRepository);
        ArgumentNullException.ThrowIfNull(challengeRepository);
        ArgumentNullException.ThrowIfNull(logger);

        this.discordClient = discordClient;
        this.poolManager = poolManager;
        this.steamGcPool = steamGcPool;
        this.playerRepository = playerRepository;
        this.matchRepository = matchRepository;
        this.challengeRepository = challengeRepository;
        this.logger = logger;
    }

    /// <summary>
    /// Connects every configured Steam bot. With one BotAccount + N PoolAccounts,
    /// this opens N+1 SteamKit connections.
    /// </summary>
    public Task<bool> StartServerAsync() => steamGcPool.StartAllAsync();

    /// <summary>
    /// Admin passthrough — uses the default (BotAccount) client. The queue-driven
    /// flow in LobbyPoolManager picks a per-match client from the pool instead.
    /// </summary>
    public void InvitePlayersToLobby(IList<ulong> steamIds)
    {
        var client = steamGcPool.Default;
        if (client == null)
        {
            logger.LogError("InvitePlayersToLobby called but no Steam clients are configured.");
            return;
        }
        client.InvitePlayersToLobby(steamIds);
    }

    /// <summary>
    /// Admin passthrough — uses the default (BotAccount) client.
    /// </summary>
    public Task CreateLobbyAsync(CreateLobbyRequest createLobbyRequest)
    {
        var client = steamGcPool.Default;
        if (client == null)
        {
            logger.LogError("CreateLobbyAsync called but no Steam clients are configured.");
            return Task.CompletedTask;
        }
        return client.CreateLobbyAsync(createLobbyRequest);
    }

    public async Task<RegisteredPlayer> RegisterPlayerAsync(RegisterPlayerRequest request, CancellationToken cancellationToken = default)
    {
        // Register only persists the player. Opt-in to matchmaking happens via JoinQueueAsync
        // (the `!queue` command). Coupling these two would mean every !register surprises the
        // user with an immediate queue entry they didn't ask for.
        var steamIdString = request.SteamId.ToString();

        var persisted = await playerRepository.UpsertAsync(new PlayerEntity
        {
            UserId = request.UserId,
            Username = request.Username,
            SteamId = steamIdString
        }, cancellationToken);

        return new RegisteredPlayer
        {
            SteamId = persisted.SteamId,
            Username = persisted.Username,
            UserId = persisted.UserId.ToString()
        };
    }

    public Task<PlayerStatsResponse?> GetPlayerProfileAsync(ulong playerId, CancellationToken cancellationToken = default) =>
        // /player/{id} used to return a sparse {Id, Name} DTO that the Discord !profile
        // formatter then expanded with missing fields (Rating, Wins, …) the server never
        // sent. Just delegate to the stats query so both endpoints return identical data.
        playerRepository.GetStatsAsync(playerId, cancellationToken);

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
                Message = NotRegisteredMessage
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

    public Task<MatchSummary?> GetMatchByLobbyAsync(string lobbyId, CancellationToken cancellationToken = default) =>
        matchRepository.GetByLobbyIdAsync(lobbyId, cancellationToken);

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

            // Close the loop with LobbyPoolManager so the Steam account is released back
            // to the pool and a new lobby is spawned for the next 10 players. Matches
            // created from 1v1 challenges have LobbyId == null and are skipped here —
            // they never reserved a pool account in the first place.
            if (!string.IsNullOrEmpty(result.Match.LobbyId))
            {
                try
                {
                    await poolManager.NotifyGameFinishedAsync(result.Match.LobbyId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to notify game finished for lobby {LobbyId}", result.Match.LobbyId);
                }
            }
        }
        return new QueueOperationResponse { Success = result.Success, Message = result.Message };
    }

    // ── Direct exposure of pool-manager session controls (admin endpoints) ──

    public List<ActiveSessionSummary> GetActiveSessions() => poolManager.GetActiveSessionSummaries();

    public Task NotifyGameStartedAsync(string lobbyId) => poolManager.NotifyGameStartedAsync(lobbyId);

    public Task NotifyGameFinishedAsync(string lobbyId) => poolManager.NotifyGameFinishedAsync(lobbyId);

    public Task DestroyLobbyAsync(string lobbyId) => poolManager.DestroyLobbyAsync(lobbyId);

    public async Task<QueueOperationResponse> LinkSteamIdAsync(ulong userId, ulong steamId, CancellationToken cancellationToken = default)
    {
        var steamIdString = steamId.ToString();

        var updated = await playerRepository.UpdateSteamIdAsync(userId, steamIdString, cancellationToken);
        if (updated == null)
        {
            return new QueueOperationResponse
            {
                Success = false,
                Message = NotRegisteredMessage
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
            return new ChallengeOperationResponse { Success = false, Message = NotRegisteredMessage };
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
