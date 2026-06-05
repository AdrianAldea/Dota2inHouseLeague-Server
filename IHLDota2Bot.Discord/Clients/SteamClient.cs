using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Discord.Models;
using System.Text.Json;

namespace IHLDota2Bot.Discord.Clients;

public class SteamClient
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

    public async Task<RegisteredPlayer?> RegisterPlayerAsync(RegisterPlayerRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/gamemanager/register", request, JsonSerializerOptions.Web, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RegisteredPlayer>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to register player {UserId}", request.UserId);
            return null;
        }
    }

    public async Task<CreateLobbyResponse?> CreateLobbyAsync(ulong id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/gamemanager/create", id, JsonSerializerOptions.Web, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CreateLobbyResponse>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create lobby for user {UserId}", id);
            return null;
        }
    }

    public async Task<ProfileResponse?> ShowProfileAsync(ulong id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<ProfileResponse>($"api/gamemanager/player/{id}", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch profile for user {UserId}", id);
            return null;
        }
    }

    public Task<QueueOperationResponse?> JoinQueueAsync(ulong id, CancellationToken cancellationToken = default) =>
        PostOperationAsync($"api/gamemanager/queue/{id}", "join queue", id, cancellationToken);

    public Task<QueueOperationResponse?> LeaveQueueAsync(ulong id, CancellationToken cancellationToken = default) =>
        PostOperationAsync($"api/gamemanager/leave/{id}", "leave queue", id, cancellationToken);

    public Task<QueueOperationResponse?> MarkReadyAsync(ulong id, CancellationToken cancellationToken = default) =>
        PostOperationAsync($"api/gamemanager/ready/{id}", "mark ready", id, cancellationToken);

    public Task<QueueOperationResponse?> MarkUnreadyAsync(ulong id, CancellationToken cancellationToken = default) =>
        PostOperationAsync($"api/gamemanager/unready/{id}", "mark unready", id, cancellationToken);

    public Task<QueueOperationResponse?> UnregisterAsync(ulong id, CancellationToken cancellationToken = default) =>
        PostOperationAsync($"api/gamemanager/unregister/{id}", "unregister", id, cancellationToken);

    public Task<QueueOperationResponse?> LinkSteamIdAsync(ulong userId, ulong steamId, CancellationToken cancellationToken = default) =>
        PostOperationAsync($"api/gamemanager/link/{userId}/{steamId}", "link Steam ID", userId, cancellationToken);

    public async Task<PlayerStatsResponse?> GetPlayerStatsAsync(ulong id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/gamemanager/stats/{id}", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PlayerStatsResponse>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch stats for user {UserId}", id);
            return null;
        }
    }

    public async Task<LeaderboardResponse?> GetLeaderboardAsync(int top, CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<LeaderboardResponse>($"api/gamemanager/leaderboard?top={top}", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch leaderboard");
            return null;
        }
    }

    public async Task<MatchSummary?> GetCurrentMatchAsync(ulong id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/gamemanager/match/current/{id}", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MatchSummary>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch current match for user {UserId}", id);
            return null;
        }
    }

    public async Task<RecentMatchesResponse?> GetRecentMatchesAsync(ulong id, int top, CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<RecentMatchesResponse>($"api/gamemanager/match/recent/{id}?top={top}", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch recent matches for user {UserId}", id);
            return null;
        }
    }

    public async Task<QueueOperationResponse?> ReportMatchAsync(ulong id, MatchTeam winner, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsync($"api/gamemanager/match/report/{id}/{winner}", content: null, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<QueueOperationResponse>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to report match for user {UserId}", id);
            return null;
        }
    }

    public Task<ChallengeOperationResponse?> ChallengePlayerAsync(ulong challengerId, ulong challengedId, CancellationToken cancellationToken = default) =>
        PostChallengeAsync($"api/gamemanager/challenge/{challengerId}/{challengedId}", "challenge", challengerId, cancellationToken);

    public Task<ChallengeOperationResponse?> AcceptChallengeAsync(ulong userId, CancellationToken cancellationToken = default) =>
        PostChallengeAsync($"api/gamemanager/challenge/accept/{userId}", "accept challenge", userId, cancellationToken);

    public Task<ChallengeOperationResponse?> DeclineChallengeAsync(ulong userId, CancellationToken cancellationToken = default) =>
        PostChallengeAsync($"api/gamemanager/challenge/decline/{userId}", "decline challenge", userId, cancellationToken);

    private async Task<ChallengeOperationResponse?> PostChallengeAsync(string path, string action, ulong userId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.PostAsync(path, content: null, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ChallengeOperationResponse>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to {Action} for user {UserId}", action, userId);
            return null;
        }
    }

    public async Task<QueueStatusResponse?> GetQueueStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<QueueStatusResponse>("api/gamemanager/queue/status", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch queue status");
            return null;
        }
    }

    private async Task<QueueOperationResponse?> PostOperationAsync(string path, string action, ulong userId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.PostAsync(path, content: null, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<QueueOperationResponse>(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to {Action} for user {UserId}", action, userId);
            return null;
        }
    }
}
