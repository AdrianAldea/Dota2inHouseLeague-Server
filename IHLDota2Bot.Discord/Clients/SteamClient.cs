using System.Text.Json;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;

namespace IHLDota2Bot.Discord.Clients;

public class SteamClient : ISteamClient
{
    public const string HttpClientName = "SteamClient";

    private readonly HttpClient httpClient;
    private readonly ILogger<SteamClient> logger;

    public SteamClient(IHttpClientFactory httpClientFactory, ILogger<SteamClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(logger);

        httpClient = httpClientFactory.CreateClient(HttpClientName);
        this.logger = logger;
    }

    /// <summary>
    /// Logs a failed Game Manager call at a level chosen by its
    /// <see cref="SteamClientErrorClassifier.FailureKind"/> and returns <c>null</c>,
    /// preserving the "null means failure" contract every caller relies on while
    /// distinguishing transient timeouts / open circuits from real transport errors.
    /// </summary>
    private TResult? LogFailure<TResult>(Exception ex, string operation)
        where TResult : class
    {
        var kind = SteamClientErrorClassifier.Classify(ex);
        logger.Log(
            SteamClientErrorClassifier.LevelFor(kind),
            ex,
            "Game Manager request failed [{Kind}] during {Operation}",
            kind,
            operation
        );
        return null;
    }

    public async Task<RegisteredPlayer?> RegisterPlayerAsync(
        RegisterPlayerRequest request,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(
                GameManagerApiRoutes.Register,
                request,
                JsonSerializerOptions.Web,
                cancellationToken
            );
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RegisteredPlayer>(cancellationToken);
        }
        catch (Exception ex)
        {
            return LogFailure<RegisteredPlayer>(ex, $"register player {request.UserId}");
        }
    }

    public Task<QueueOperationResponse?> JoinQueueAsync(
        ulong id,
        CancellationToken cancellationToken = default
    ) => PostOperationAsync(GameManagerApiRoutes.Queue(id), "join queue", id, cancellationToken);

    public Task<QueueOperationResponse?> LeaveQueueAsync(
        ulong id,
        CancellationToken cancellationToken = default
    ) => PostOperationAsync(GameManagerApiRoutes.Leave(id), "leave queue", id, cancellationToken);

    public Task<QueueOperationResponse?> MarkReadyAsync(
        ulong id,
        CancellationToken cancellationToken = default
    ) => PostOperationAsync(GameManagerApiRoutes.Ready(id), "mark ready", id, cancellationToken);

    public Task<QueueOperationResponse?> MarkUnreadyAsync(
        ulong id,
        CancellationToken cancellationToken = default
    ) =>
        PostOperationAsync(GameManagerApiRoutes.Unready(id), "mark unready", id, cancellationToken);

    public Task<QueueOperationResponse?> UnregisterAsync(
        ulong id,
        CancellationToken cancellationToken = default
    ) =>
        PostOperationAsync(
            GameManagerApiRoutes.Unregister(id),
            "unregister",
            id,
            cancellationToken
        );

    public Task<QueueOperationResponse?> LinkSteamIdAsync(
        ulong userId,
        ulong steamId,
        CancellationToken cancellationToken = default
    ) =>
        PostOperationAsync(
            GameManagerApiRoutes.Link(userId, steamId),
            "link Steam ID",
            userId,
            cancellationToken
        );

    public async Task<PlayerStatsResponse?> GetPlayerStatsAsync(
        ulong id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await httpClient.GetAsync(
                GameManagerApiRoutes.Stats(id),
                cancellationToken
            );
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PlayerStatsResponse>(cancellationToken);
        }
        catch (Exception ex)
        {
            return LogFailure<PlayerStatsResponse>(ex, $"fetch stats for user {id}");
        }
    }

    public async Task<LeaderboardResponse?> GetLeaderboardAsync(
        int top,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await httpClient.GetFromJsonAsync<LeaderboardResponse>(
                GameManagerApiRoutes.Leaderboard(top),
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            return LogFailure<LeaderboardResponse>(ex, "fetch leaderboard");
        }
    }

    public async Task<MatchSummary?> GetCurrentMatchAsync(
        ulong id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await httpClient.GetAsync(
                GameManagerApiRoutes.CurrentMatch(id),
                cancellationToken
            );
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MatchSummary>(cancellationToken);
        }
        catch (Exception ex)
        {
            return LogFailure<MatchSummary>(ex, $"fetch current match for user {id}");
        }
    }

    public async Task<RecentMatchesResponse?> GetRecentMatchesAsync(
        ulong id,
        int top,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await httpClient.GetFromJsonAsync<RecentMatchesResponse>(
                GameManagerApiRoutes.RecentMatches(id, top),
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            return LogFailure<RecentMatchesResponse>(ex, $"fetch recent matches for user {id}");
        }
    }

    public async Task<QueueOperationResponse?> ReportMatchAsync(
        ulong id,
        MatchTeam winner,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await httpClient.PostAsync(
                GameManagerApiRoutes.ReportMatch(id, winner),
                content: null,
                cancellationToken
            );
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<QueueOperationResponse>(
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            return LogFailure<QueueOperationResponse>(ex, $"report match for user {id}");
        }
    }

    public Task<ChallengeOperationResponse?> ChallengePlayerAsync(
        ulong challengerId,
        ulong challengedId,
        CancellationToken cancellationToken = default
    ) =>
        PostChallengeAsync(
            GameManagerApiRoutes.Challenge(challengerId, challengedId),
            "challenge",
            challengerId,
            cancellationToken
        );

    public Task<ChallengeOperationResponse?> AcceptChallengeAsync(
        ulong userId,
        CancellationToken cancellationToken = default
    ) =>
        PostChallengeAsync(
            GameManagerApiRoutes.AcceptChallenge(userId),
            "accept challenge",
            userId,
            cancellationToken
        );

    public Task<ChallengeOperationResponse?> DeclineChallengeAsync(
        ulong userId,
        CancellationToken cancellationToken = default
    ) =>
        PostChallengeAsync(
            GameManagerApiRoutes.DeclineChallenge(userId),
            "decline challenge",
            userId,
            cancellationToken
        );

    private async Task<ChallengeOperationResponse?> PostChallengeAsync(
        string path,
        string action,
        ulong userId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await httpClient.PostAsync(path, content: null, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ChallengeOperationResponse>(
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            return LogFailure<ChallengeOperationResponse>(ex, $"{action} for user {userId}");
        }
    }

    public async Task<QueueStatusResponse?> GetQueueStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await httpClient.GetFromJsonAsync<QueueStatusResponse>(
                GameManagerApiRoutes.QueueStatus,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            return LogFailure<QueueStatusResponse>(ex, "fetch queue status");
        }
    }

    private async Task<QueueOperationResponse?> PostOperationAsync(
        string path,
        string action,
        ulong userId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await httpClient.PostAsync(path, content: null, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<QueueOperationResponse>(
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            return LogFailure<QueueOperationResponse>(ex, $"{action} for user {userId}");
        }
    }

    public Task<QueueOperationResponse?> BanPlayerAsync(
        ulong id,
        string? reason,
        CancellationToken cancellationToken = default
    )
    {
        var path = GameManagerApiRoutes.Ban(id);
        if (!string.IsNullOrEmpty(reason))
            path += $"?reason={Uri.EscapeDataString(reason)}";
        return PostOperationAsync(path, "ban player", id, cancellationToken);
    }

    public Task<QueueOperationResponse?> UnbanPlayerAsync(
        ulong id,
        CancellationToken cancellationToken = default
    ) => PostOperationAsync(GameManagerApiRoutes.Unban(id), "unban player", id, cancellationToken);

    public Task<QueueOperationResponse?> KickPlayerAsync(
        ulong id,
        CancellationToken cancellationToken = default
    ) => PostOperationAsync(GameManagerApiRoutes.Kick(id), "kick player", id, cancellationToken);

    public Task<QueueOperationResponse?> ForceStartLobbyAsync(
        CancellationToken cancellationToken = default
    ) => PostOperationAsync(GameManagerApiRoutes.ForceStart, "force start", 0, cancellationToken);

    public Task<QueueOperationResponse?> ResetQueueAsync(
        CancellationToken cancellationToken = default
    ) => PostOperationAsync(GameManagerApiRoutes.ResetQueue, "reset queue", 0, cancellationToken);

    public async Task<QueueOperationResponse?> AddSteamAccountAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(
                GameManagerApiRoutes.AddAccount,
                new AddSteamAccountRequest { Username = username, Password = password },
                JsonSerializerOptions.Web,
                cancellationToken
            );
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<QueueOperationResponse>(
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            return LogFailure<QueueOperationResponse>(ex, $"add Steam account {username}");
        }
    }

    public async Task<RankResponse?> GetPlayerRankAsync(
        ulong id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await httpClient.GetAsync(
                GameManagerApiRoutes.Rank(id),
                cancellationToken
            );
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RankResponse>(cancellationToken);
        }
        catch (Exception ex)
        {
            return LogFailure<RankResponse>(ex, $"fetch rank for user {id}");
        }
    }

    public async Task<MatchSummary?> GetMatchByIdAsync(
        Guid matchId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var response = await httpClient.GetAsync(
                GameManagerApiRoutes.MatchById(matchId),
                cancellationToken
            );
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MatchSummary>(cancellationToken);
        }
        catch (Exception ex)
        {
            return LogFailure<MatchSummary>(ex, $"fetch match {matchId}");
        }
    }

    public async Task<PoolStatusResponse?> GetPoolStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await httpClient.GetFromJsonAsync<PoolStatusResponse>(
                GameManagerApiRoutes.PoolStatus,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            return LogFailure<PoolStatusResponse>(ex, "fetch pool status");
        }
    }
}
