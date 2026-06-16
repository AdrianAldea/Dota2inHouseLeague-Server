using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.IntegrationTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// Extended contract coverage for Discord bot's <see cref="SteamClient"/> ↔ Game
/// Manager API boundary: challenge/accept/decline/report flows, ban/unban, rank,
/// recent matches, queue status, and unregister. Complements the happy-path smoke
/// tests in <see cref="DiscordSteamClientIntegrationTests"/> with broader endpoint
/// coverage so that any endpoint shape or DTO serialization change breaks at least
/// one test here.
/// </summary>
public class DiscordSteamClientContractTests : IntegrationTestBase
{
    private SteamClient CreateSteamClient() =>
        new(new SingleClientHttpFactory(Factory.CreateClient()), NullLogger<SteamClient>.Instance);

    private async Task RegisterAsync(SteamClient steam, ulong userId, string username) =>
        await steam.RegisterPlayerAsync(
            new RegisterPlayerRequest { SteamId = 76561198000000000 + userId, UserId = userId, Username = username });

    // ── Challenge / accept / decline / report ─────────────────────────────────

    [Fact]
    public async Task ChallengeAndAccept_CreatesInProgressMatch()
    {
        var steam = CreateSteamClient();
        await RegisterAsync(steam, 1, "alice");
        await RegisterAsync(steam, 2, "bob");

        var challenge = await steam.ChallengePlayerAsync(1, 2);
        Assert.True(challenge!.Success);

        var accept = await steam.AcceptChallengeAsync(2);
        Assert.True(accept!.Success);
        Assert.NotNull(accept.Match);
        Assert.Equal(MatchStatus.InProgress, accept.Match!.Status);
    }

    [Fact]
    public async Task GetCurrentMatch_AfterAccept_ReturnsMatch()
    {
        var steam = CreateSteamClient();
        await RegisterAsync(steam, 1, "alice");
        await RegisterAsync(steam, 2, "bob");
        await steam.ChallengePlayerAsync(1, 2);
        await steam.AcceptChallengeAsync(2);

        var current = await steam.GetCurrentMatchAsync(1);

        Assert.NotNull(current);
        Assert.Equal(MatchStatus.InProgress, current!.Status);
    }

    [Fact]
    public async Task DeclineChallenge_ClearsChallenge_AcceptThenFails()
    {
        var steam = CreateSteamClient();
        await RegisterAsync(steam, 1, "alice");
        await RegisterAsync(steam, 2, "bob");
        await steam.ChallengePlayerAsync(1, 2);

        var decline = await steam.DeclineChallengeAsync(2);
        Assert.True(decline!.Success);

        // No pending challenge remains for bob.
        var accept = await steam.AcceptChallengeAsync(2);
        Assert.False(accept!.Success);
    }

    [Fact]
    public async Task ReportMatch_AfterChallengeAccept_AppliesEloAndSettlesMatch()
    {
        var steam = CreateSteamClient();
        await RegisterAsync(steam, 1, "alice");
        await RegisterAsync(steam, 2, "bob");
        await steam.ChallengePlayerAsync(1, 2);
        await steam.AcceptChallengeAsync(2);

        var report = await steam.ReportMatchAsync(1, MatchTeam.Radiant);

        Assert.True(report!.Success);
        Assert.Contains("Radiant", report.Message, StringComparison.OrdinalIgnoreCase);

        // Winner gains ELO.
        var aliceStats = await steam.GetPlayerStatsAsync(1);
        Assert.True(aliceStats!.Rating > 1000);
    }

    [Fact]
    public async Task GetRecentMatches_AfterCompletedMatch_ReturnsOne()
    {
        var steam = CreateSteamClient();
        await RegisterAsync(steam, 1, "alice");
        await RegisterAsync(steam, 2, "bob");
        await steam.ChallengePlayerAsync(1, 2);
        await steam.AcceptChallengeAsync(2);
        await steam.ReportMatchAsync(1, MatchTeam.Radiant);

        var recent = await steam.GetRecentMatchesAsync(1, 5);

        Assert.NotNull(recent);
        Assert.Equal(1, recent!.Count);
        Assert.All(recent.Matches, m => Assert.Equal(MatchStatus.Completed, m.Status));
    }

    [Fact]
    public async Task GetMatchById_AfterChallengeAccept_ReturnsMatchDetails()
    {
        var steam = CreateSteamClient();
        await RegisterAsync(steam, 1, "alice");
        await RegisterAsync(steam, 2, "bob");
        await steam.ChallengePlayerAsync(1, 2);
        var accepted = await steam.AcceptChallengeAsync(2);
        var matchId = accepted!.Match!.Id;

        var retrieved = await steam.GetMatchByIdAsync(matchId);

        Assert.NotNull(retrieved);
        Assert.Equal(matchId, retrieved!.Id);
        Assert.Equal(2, retrieved.Players.Count);
    }

    // ── Player lifecycle ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetPlayerRank_AfterRegister_ReturnsRankOneOfOne()
    {
        var steam = CreateSteamClient();
        await RegisterAsync(steam, 1, "alice");

        var rank = await steam.GetPlayerRankAsync(1);

        Assert.NotNull(rank);
        Assert.Equal(1, rank!.Rank);
        Assert.Equal(1, rank.TotalPlayers);
    }

    [Fact]
    public async Task UnregisterPlayer_RemovesFromSystem()
    {
        var steam = CreateSteamClient();
        await RegisterAsync(steam, 1, "alice");

        var unreg = await steam.UnregisterAsync(1);
        Assert.True(unreg!.Success);

        // Stats endpoint now returns null (404 → null by contract).
        var stats = await steam.GetPlayerStatsAsync(1);
        Assert.Null(stats);
    }

    // ── Admin operations ──────────────────────────────────────────────────────

    [Fact]
    public async Task BanAndUnban_RoundTrips()
    {
        var steam = CreateSteamClient();
        await RegisterAsync(steam, 1, "alice");

        var ban = await steam.BanPlayerAsync(1, "test ban");
        Assert.True(ban!.Success);

        var unban = await steam.UnbanPlayerAsync(1);
        Assert.True(unban!.Success);
    }

    [Fact]
    public async Task KickPlayer_WhenNotInQueue_ReturnsFailure()
    {
        var steam = CreateSteamClient();
        await RegisterAsync(steam, 1, "alice");

        var kick = await steam.KickPlayerAsync(1);

        // alice is registered but not in the queue — kick should fail gracefully.
        Assert.NotNull(kick);
        Assert.False(kick!.Success);
    }

    // ── Queue status ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetQueueStatus_ReturnsWellFormedResponse()
    {
        var steam = CreateSteamClient();

        var status = await steam.GetQueueStatusAsync();

        Assert.NotNull(status);
        Assert.Equal(0, status!.CurrentLobbyPlayerCount);
        Assert.True(status.PlayersPerLobby > 0);
    }
}
