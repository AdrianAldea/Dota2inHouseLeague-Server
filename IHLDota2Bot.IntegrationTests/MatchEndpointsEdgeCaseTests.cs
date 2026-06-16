using System.Net;
using System.Net.Http.Json;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.IntegrationTests.TestSupport;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// Edge and error cases for MatchEndpoints that go beyond the happy-path lifecycle
/// covered in <see cref="MatchFlowApiTests"/>: 404 on unknown resources, rejection of
/// duplicate reports, top-N limiting, and unregistered-player guards.
/// </summary>
public class MatchEndpointsEdgeCaseTests : IntegrationTestBase
{
    private async Task RegisterAsync(ulong userId, string username) =>
        (await Client.PostAsJsonAsync("/api/gamemanager/register",
            new RegisterPlayerRequest { SteamId = 76561198000000000 + userId, UserId = userId, Username = username }))
        .EnsureSuccessStatusCode();

    private async Task<T> PostJson<T>(string path) =>
        (await (await Client.PostAsync(path, content: null)).Content.ReadFromJsonAsync<T>())!;

    private async Task ChallengeAndAcceptAsync(ulong challengerId, ulong challengedId)
    {
        await PostJson<ChallengeOperationResponse>($"/api/gamemanager/challenge/{challengerId}/{challengedId}");
        await PostJson<ChallengeOperationResponse>($"/api/gamemanager/challenge/accept/{challengedId}");
    }

    [Fact]
    public async Task GetMatchByLobby_UnknownLobbyId_Returns404()
    {
        var response = await Client.GetAsync("/api/gamemanager/match/by-lobby/nonexistent-lobby-xyz");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentMatch_UnregisteredPlayer_Returns404()
    {
        var response = await Client.GetAsync("/api/gamemanager/match/current/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Report_OnAlreadySettledMatch_ReturnsFailure()
    {
        await RegisterAsync(1, "alice");
        await RegisterAsync(2, "bob");
        await ChallengeAndAcceptAsync(1, 2);

        var first = await PostJson<QueueOperationResponse>("/api/gamemanager/match/report/1/radiant");
        Assert.True(first.Success);

        // Match is now settled; a second report must fail — alice has no active match.
        var second = await PostJson<QueueOperationResponse>("/api/gamemanager/match/report/1/radiant");
        Assert.False(second.Success);
    }

    [Fact]
    public async Task GetRecentMatches_WithTopFilter_LimitsResults()
    {
        await RegisterAsync(1, "alice");
        await RegisterAsync(2, "bob");

        // Play two matches.
        await ChallengeAndAcceptAsync(1, 2);
        await PostJson<QueueOperationResponse>("/api/gamemanager/match/report/1/radiant");

        await ChallengeAndAcceptAsync(1, 2);
        await PostJson<QueueOperationResponse>("/api/gamemanager/match/report/2/dire");

        var top1 = await Client.GetFromJsonAsync<RecentMatchesResponse>("/api/gamemanager/match/recent/1?top=1");
        var top2 = await Client.GetFromJsonAsync<RecentMatchesResponse>("/api/gamemanager/match/recent/1?top=2");

        Assert.Equal(1, top1!.Count);
        Assert.Equal(2, top2!.Count);
    }

    [Fact]
    public async Task GetRecentMatches_UnknownPlayer_ReturnsEmptyList()
    {
        var result = await Client.GetFromJsonAsync<RecentMatchesResponse>("/api/gamemanager/match/recent/999?top=5");

        Assert.NotNull(result);
        Assert.Equal(0, result!.Count);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task GetMatchById_AfterChallengeMatch_ReturnsMatchDetails()
    {
        await RegisterAsync(1, "alice");
        await RegisterAsync(2, "bob");

        await PostJson<ChallengeOperationResponse>("/api/gamemanager/challenge/1/2");
        var accepted = await PostJson<ChallengeOperationResponse>("/api/gamemanager/challenge/accept/2");
        var matchId = accepted.Match!.Id;

        var retrieved = await Client.GetFromJsonAsync<MatchSummary>($"/api/gamemanager/match/{matchId}");

        Assert.NotNull(retrieved);
        Assert.Equal(matchId, retrieved!.Id);
        Assert.Equal(MatchStatus.InProgress, retrieved.Status);
        Assert.Equal(2, retrieved.Players.Count);
    }
}
