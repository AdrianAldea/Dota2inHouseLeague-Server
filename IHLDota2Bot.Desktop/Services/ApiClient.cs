using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;

namespace IHLDota2Bot.Desktop.Services;

/// <summary>
/// The single place that knows the backend HTTP API — the C# counterpart of the web
/// app's <c>game-manager.service.ts</c>. Returns the shared <c>IHLDota2Bot.Common</c>
/// DTOs. The Bearer token is attached automatically by <see cref="AuthHeaderHandler"/>.
///
/// DI sets <c>BaseAddress</c> to <c>{ApiBaseUrl}/api/</c>; paths below add the
/// <c>gamemanager/</c> or <c>auth/</c> group as needed.
/// </summary>
public sealed class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── Public reads (no identity required) ──

    public Task<LeaderboardResponse?> GetLeaderboardAsync(
        int top = 10,
        CancellationToken ct = default
    ) =>
        http.GetFromJsonAsync<LeaderboardResponse>(
            $"gamemanager/leaderboard?top={top}",
            JsonOptions,
            ct
        );

    public Task<QueueStatusResponse?> GetQueueStatusAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<QueueStatusResponse>("gamemanager/queue/status", JsonOptions, ct);

    // ── Identity / auth ──

    public Task<MeResponse?> GetMeAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<MeResponse>("auth/me", JsonOptions, ct);

    public async Task<AuthResultResponse?> RegisterAsync(
        string username,
        CancellationToken ct = default
    )
    {
        using var response = await http.PostAsJsonAsync(
            "auth/register",
            new RegisterAccountRequest { Username = username },
            JsonOptions,
            ct
        );
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthResultResponse>(JsonOptions, ct);
    }

    // ── "Me" reads (act as the signed-in player) ──

    public Task<PlayerStatsResponse?> GetMyStatsAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<PlayerStatsResponse>("gamemanager/me/stats", JsonOptions, ct);

    public Task<RankResponse?> GetMyRankAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<RankResponse>("gamemanager/me/rank", JsonOptions, ct);

    public Task<RecentMatchesResponse?> GetMyMatchesAsync(
        int top = 10,
        CancellationToken ct = default
    ) =>
        http.GetFromJsonAsync<RecentMatchesResponse>(
            $"gamemanager/me/matches?top={top}",
            JsonOptions,
            ct
        );

    /// <summary>The signed-in player's current match, or null when there isn't one (404).</summary>
    public async Task<CurrentMatchResponse?> GetMyMatchAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("gamemanager/me/match", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CurrentMatchResponse>(JsonOptions, ct);
    }

    // ── "Me" actions ──

    public Task<QueueOperationResponse?> JoinQueueAsync(CancellationToken ct = default) =>
        PostMe("gamemanager/queue/join", ct);

    public Task<QueueOperationResponse?> LeaveQueueAsync(CancellationToken ct = default) =>
        PostMe("gamemanager/queue/leave", ct);

    public Task<QueueOperationResponse?> ReadyAsync(CancellationToken ct = default) =>
        PostMe("gamemanager/ready", ct);

    public Task<QueueOperationResponse?> UnreadyAsync(CancellationToken ct = default) =>
        PostMe("gamemanager/unready", ct);

    private async Task<QueueOperationResponse?> PostMe(string path, CancellationToken ct)
    {
        using var response = await http.PostAsync(path, content: null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QueueOperationResponse>(JsonOptions, ct);
    }
}
