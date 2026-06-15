using System.Net;
using System.Net.Http.Json;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.IntegrationTests.TestSupport;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// The full match lifecycle over real HTTP, end to end through the whole stack:
/// challenge → accept (creates a 1v1 InProgress match) → report → ELO + W/L applied.
/// This exercises endpoints, Dota2Service, the player/challenge/match repositories,
/// EloCalculator and EF together — no Steam/Discord required (challenge matches have
/// no lobby, so the Steam pool is never touched).
/// </summary>
public class MatchFlowApiTests : IntegrationTestBase
{
    private async Task RegisterAsync(ulong userId, string username) =>
        (await Client.PostAsJsonAsync("/api/gamemanager/register",
            new RegisterPlayerRequest { SteamId = 76561198000000000 + userId, UserId = userId, Username = username }))
        .EnsureSuccessStatusCode();

    private async Task<T> PostJson<T>(string path) =>
        (await (await Client.PostAsync(path, content: null)).Content.ReadFromJsonAsync<T>())!;

    [Fact]
    public async Task ChallengeAcceptReport_AppliesEloAndWinLoss()
    {
        await RegisterAsync(1, "alice");
        await RegisterAsync(2, "bob");

        // alice challenges bob
        var challenge = await PostJson<ChallengeOperationResponse>("/api/gamemanager/challenge/1/2");
        Assert.True(challenge.Success);
        Assert.NotNull(challenge.Challenge);

        // bob accepts → a 1v1 match is created (challenger alice = Radiant, bob = Dire)
        var accept = await PostJson<ChallengeOperationResponse>("/api/gamemanager/challenge/accept/2");
        Assert.True(accept.Success);
        Assert.NotNull(accept.Match);
        Assert.Equal(MatchStatus.InProgress, accept.Match!.Status);
        Assert.Equal(MatchTeam.Radiant, accept.Match.Players.Single(p => p.UserId == 1).Team);
        Assert.Equal(MatchTeam.Dire, accept.Match.Players.Single(p => p.UserId == 2).Team);

        // alice is in an active match
        var current = await Client.GetFromJsonAsync<MatchSummary>("/api/gamemanager/match/current/1");
        Assert.Equal(MatchStatus.InProgress, current!.Status);

        // report Radiant (alice) as the winner
        var report = await PostJson<QueueOperationResponse>("/api/gamemanager/match/report/1/radiant");
        Assert.True(report.Success);
        Assert.Contains("Winner: Radiant", report.Message);

        // Even 1000v1000 match → winner +16, loser -16.
        var alice = await Client.GetFromJsonAsync<PlayerStatsResponse>("/api/gamemanager/stats/1");
        var bob = await Client.GetFromJsonAsync<PlayerStatsResponse>("/api/gamemanager/stats/2");
        Assert.Equal(1016, alice!.Rating);
        Assert.Equal(1, alice.Wins);
        Assert.Equal(984, bob!.Rating);
        Assert.Equal(1, bob.Losses);

        // The settled match shows up in recent history, and is no longer "current".
        var recent = await Client.GetFromJsonAsync<RecentMatchesResponse>("/api/gamemanager/match/recent/1?top=5");
        Assert.Equal(1, recent!.Count);
        Assert.All(recent.Matches, m => Assert.Equal(MatchStatus.Completed, m.Status));

        var noCurrent = await Client.GetAsync("/api/gamemanager/match/current/1");
        Assert.Equal(HttpStatusCode.NotFound, noCurrent.StatusCode);
    }

    [Fact]
    public async Task ChallengeYourself_IsRefused()
    {
        await RegisterAsync(1, "alice");

        var result = await PostJson<ChallengeOperationResponse>("/api/gamemanager/challenge/1/1");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Accept_NoPendingChallenge_Fails()
    {
        await RegisterAsync(1, "alice");

        var result = await PostJson<ChallengeOperationResponse>("/api/gamemanager/challenge/accept/1");

        Assert.False(result.Success);
        Assert.Contains("No pending challenge", result.Message);
    }

    [Fact]
    public async Task Decline_PendingChallenge_Succeeds()
    {
        await RegisterAsync(1, "alice");
        await RegisterAsync(2, "bob");
        await PostJson<ChallengeOperationResponse>("/api/gamemanager/challenge/1/2");

        var decline = await PostJson<ChallengeOperationResponse>("/api/gamemanager/challenge/decline/2");

        Assert.True(decline.Success);

        // After declining there is nothing left to accept.
        var accept = await PostJson<ChallengeOperationResponse>("/api/gamemanager/challenge/accept/2");
        Assert.False(accept.Success);
    }

    [Fact]
    public async Task Report_NoActiveMatch_ReturnsFailure()
    {
        await RegisterAsync(1, "alice");

        var report = await PostJson<QueueOperationResponse>("/api/gamemanager/match/report/1/radiant");

        Assert.False(report.Success);
        Assert.Contains("active match", report.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Report_InvalidWinner_Returns400()
    {
        var response = await Client.PostAsync("/api/gamemanager/match/report/1/banana", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMatch_UnknownId_Returns404()
    {
        var response = await Client.GetAsync($"/api/gamemanager/match/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
