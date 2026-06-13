using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Discord.Helpers;

namespace IHLDota2Bot.Discord.Tests;

/// <summary>
/// <see cref="MessagesFormatter"/> turns API DTOs into the Markdown the bot posts
/// to Discord. Pure functions — these assert the real rendering rules: win-rate /
/// percentile placeholders for the "no data" cases, team grouping, ready markers,
/// and the empty-collection messages.
/// </summary>
public class MessagesFormatterTests
{
    private static PlayerStatsResponse Stats(int wins, int losses) => new()
    {
        UserId = 1,
        Username = "alice",
        SteamId = "steam-1",
        Rating = 1100,
        Wins = wins,
        Losses = losses,
    };

    private static MatchPlayerSummary P(ulong id, string name, MatchTeam team, bool won) =>
        new() { UserId = id, Username = name, Team = team, Won = won };

    // ── FormatRank ──

    [Fact]
    public void FormatRank_RendersRankPercentileAndWinRate()
    {
        var rank = new RankResponse { Rank = 3, TotalPlayers = 4, Percentile = 75, Stats = Stats(3, 1) };

        var text = MessagesFormatter.FormatRank(rank);

        Assert.Contains("alice", text);
        Assert.Contains("#3", text);
        Assert.Contains("of `4`", text);
        Assert.Contains("top 75.0%", text);
        Assert.Contains("75.0%", text); // win rate (3/4)
    }

    [Fact]
    public void FormatRank_SinglePlayer_ShowsDashForPercentile()
    {
        var rank = new RankResponse { Rank = 1, TotalPlayers = 1, Percentile = 0, Stats = Stats(0, 0) };

        var text = MessagesFormatter.FormatRank(rank);

        Assert.Contains("(—)", text);    // percentile placeholder
        Assert.Contains("`—`", text);    // win-rate placeholder (no matches)
    }

    // ── FormatPlayerStats ──

    [Fact]
    public void FormatPlayerStats_NoMatches_ShowsDashWinRate()
    {
        var text = MessagesFormatter.FormatPlayerStats(Stats(0, 0));

        Assert.Contains("steam-1", text);
        Assert.Contains("Matches: `0`", text);
        Assert.Contains("Win rate: `—`", text);
    }

    [Fact]
    public void FormatPlayerStats_WithMatches_ShowsPercentWinRate()
    {
        var text = MessagesFormatter.FormatPlayerStats(Stats(3, 1));

        Assert.Contains("Win rate: `75.0%`", text);
    }

    // ── FormatLeaderboard ──

    [Fact]
    public void FormatLeaderboard_Empty_ShowsPlaceholder()
    {
        var text = MessagesFormatter.FormatLeaderboard(new LeaderboardResponse { TopN = 10, Entries = [] });

        Assert.Equal("_No players on the leaderboard yet._", text);
    }

    [Fact]
    public void FormatLeaderboard_ListsEachEntry()
    {
        var board = new LeaderboardResponse
        {
            TopN = 2,
            Entries =
            [
                new LeaderboardEntry { Rank = 1, UserId = 1, Username = "alice", Rating = 1300, Wins = 3, Losses = 1 },
                new LeaderboardEntry { Rank = 2, UserId = 2, Username = "bob", Rating = 1200, Wins = 0, Losses = 0 },
            ],
        };

        var text = MessagesFormatter.FormatLeaderboard(board);

        Assert.Contains("top 2", text);
        Assert.Contains("alice", text);
        Assert.Contains("75%", text); // 3/4 rendered with no decimals
        Assert.Contains("bob", text);
        Assert.Contains("(—)", text); // bob has no matches
    }

    // ── FormatPoolStatus ──

    [Fact]
    public void FormatPoolStatus_NoAccounts_ShowsPlaceholderAndCounts()
    {
        var status = new PoolStatusResponse { Total = 0, Ready = 0, InUse = 0, OnCooldown = 0, Accounts = [] };

        var text = MessagesFormatter.FormatPoolStatus(status);

        Assert.Contains("Total: `0`", text);
        Assert.Contains("_No accounts registered._", text);
    }

    [Fact]
    public void FormatPoolStatus_RendersAccountStateLobbyAndLastUsed()
    {
        var status = new PoolStatusResponse
        {
            Total = 1,
            Ready = 1,
            InUse = 0,
            OnCooldown = 0,
            Accounts =
            [
                new PoolAccountSummary { Username = "bot1", State = PoolAccountState.Ready, CurrentLobbyId = null, LastUsedAt = null },
            ],
        };

        var text = MessagesFormatter.FormatPoolStatus(status);

        Assert.Contains("bot1", text);
        Assert.Contains("Ready", text);
        Assert.Contains("lobby `—`", text);   // no lobby
        Assert.Contains("last used `never`", text);
    }

    // ── FormatMatch ──

    [Fact]
    public void FormatMatch_GroupsPlayersByTeamAndShowsViewerSide()
    {
        var match = new MatchSummary
        {
            Id = Guid.NewGuid(),
            LobbyId = "lobby-1",
            CreatedAt = new DateTime(2026, 6, 13, 10, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 6, 13, 10, 45, 0, DateTimeKind.Utc),
            Status = MatchStatus.Completed,
            WinningTeam = MatchTeam.Radiant,
            Players = [P(1, "alice", MatchTeam.Radiant, true), P(2, "bob", MatchTeam.Dire, false)],
        };

        var text = MessagesFormatter.FormatMatch(match, viewerUserId: 2);

        Assert.Contains("**Radiant**", text);
        Assert.Contains("**Dire**", text);
        Assert.Contains("alice", text);
        Assert.Contains("bob", text);
        Assert.Contains("Your team: **Dire**", text);      // viewer 2 is on Dire
        Assert.Contains("Winner: **Radiant**", text);
    }

    [Fact]
    public void FormatMatch_NonParticipantViewer_OmitsYourTeamLine()
    {
        var match = new MatchSummary
        {
            Id = Guid.NewGuid(),
            LobbyId = null,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = null,
            Status = MatchStatus.InProgress,
            WinningTeam = null,
            Players = [P(1, "alice", MatchTeam.Radiant, false)],
        };

        var text = MessagesFormatter.FormatMatch(match, viewerUserId: 999);

        Assert.DoesNotContain("Your team", text);
    }

    // ── FormatRecentMatches ──

    [Fact]
    public void FormatRecentMatches_Empty_ShowsPlaceholder()
    {
        var recent = new RecentMatchesResponse { UserId = 1, Count = 0, Matches = [] };

        Assert.Equal("_No completed matches yet._", MessagesFormatter.FormatRecentMatches(recent, 1));
    }

    [Fact]
    public void FormatRecentMatches_MarksWinOrLossFromViewerPerspective()
    {
        var win = new MatchSummary
        {
            Id = Guid.NewGuid(),
            LobbyId = "l1",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Status = MatchStatus.Completed,
            WinningTeam = MatchTeam.Radiant,
            Players = [P(1, "alice", MatchTeam.Radiant, true), P(2, "bob", MatchTeam.Dire, false)],
        };
        var recent = new RecentMatchesResponse { UserId = 1, Count = 1, Matches = [win] };

        Assert.Contains("WIN", MessagesFormatter.FormatRecentMatches(recent, 1));
        Assert.Contains("LOSS", MessagesFormatter.FormatRecentMatches(recent, 2));
    }

    // ── FormatQueueStatus ──

    [Fact]
    public void FormatQueueStatus_EmptyLobby_ShowsPlaceholder()
    {
        var status = new QueueStatusResponse
        {
            CurrentLobbyId = "lobby-1",
            CurrentLobbyPlayerCount = 0,
            PlayersPerLobby = 10,
            PlayersReady = 0,
            TotalRegistered = 0,
            WaitingPlayers = 0,
            Players = [],
        };

        var text = MessagesFormatter.FormatQueueStatus(status);

        Assert.Contains("Lobby: `0/10`", text);
        Assert.Contains("_No players in the current lobby._", text);
    }

    [Fact]
    public void FormatQueueStatus_ShowsReadyMarkers()
    {
        var status = new QueueStatusResponse
        {
            CurrentLobbyId = "lobby-1",
            CurrentLobbyPlayerCount = 2,
            PlayersPerLobby = 10,
            PlayersReady = 1,
            TotalRegistered = 2,
            WaitingPlayers = 0,
            Players =
            [
                new QueuePlayerSummary { Username = "alice", IsReady = true },
                new QueuePlayerSummary { Username = "bob", IsReady = false },
            ],
        };

        var text = MessagesFormatter.FormatQueueStatus(status);

        Assert.Contains("`[R]` alice", text);
        Assert.Contains("`[ ]` bob", text);
    }
}
