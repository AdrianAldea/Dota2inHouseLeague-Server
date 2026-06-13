using IHLDota2Bot;
using Microsoft.Extensions.Logging;
using Moq;

namespace IHLDota2Bot.Tests;

/// <summary>
/// Behavioural tests for the pure-domain <see cref="LobbyQueue"/>. The only
/// dependency is an <see cref="ILogger{T}"/>, supplied here as a Moq stub. Tests
/// are written against <see cref="LeagueConstants.PlayersPerLobby"/> rather than a
/// hard-coded capacity so they stay correct if the lobby size is retuned.
/// </summary>
public class LobbyQueueTests
{
    private static readonly int Capacity = LeagueConstants.PlayersPerLobby;

    private static LobbyQueue CreateQueue() => new(Mock.Of<ILogger<LobbyQueue>>());

    /// <summary>Joins <paramref name="count"/> distinct users, returning their ids.</summary>
    private static async Task<List<ulong>> FillAsync(LobbyQueue queue, int count)
    {
        var ids = new List<ulong>();
        for (var i = 0; i < count; i++)
        {
            ulong id = (ulong)(i + 1);
            await queue.JoinQueueAsync(id, $"user{id}", $"steam-{id}");
            ids.Add(id);
        }
        return ids;
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new LobbyQueue(null!));
    }

    [Fact]
    public async Task JoinQueue_NewPlayer_SucceedsRegistersAndRaisesPlayerJoined()
    {
        var queue = CreateQueue();
        PlayerJoinedEventArgs? raised = null;
        queue.PlayerJoined += (_, e) => raised = e;

        var result = await queue.JoinQueueAsync(42, "alice", "steam-42");

        Assert.True(result.Success);
        Assert.Contains("alice", result.Message);
        Assert.NotNull(raised);
        Assert.Equal(42UL, raised!.Player.UserId);

        var registered = Assert.Single(queue.GetRegisteredPlayers());
        Assert.Equal("alice", registered.Username);
        Assert.NotNull(registered.CurrentLobbyId);
    }

    [Fact]
    public async Task JoinQueue_SamePlayerAlreadyInLobby_IsRejected()
    {
        var queue = CreateQueue();
        await queue.JoinQueueAsync(1, "alice", "steam-1");

        var second = await queue.JoinQueueAsync(1, "alice", "steam-1");

        Assert.False(second.Success);
        Assert.Contains("already in lobby", second.Message);
        Assert.Single(queue.GetRegisteredPlayers()); // not double-registered
    }

    [Fact]
    public async Task JoinQueue_WhenLobbyFull_RejectsAdditionalPlayer()
    {
        var queue = CreateQueue();
        await FillAsync(queue, Capacity);

        var overflow = await queue.JoinQueueAsync(9999, "latecomer", "steam-9999");

        Assert.False(overflow.Success);
        Assert.Contains("full", overflow.Message);
    }

    [Fact]
    public async Task JoinQueue_ReachingCapacity_RaisesLobbyFull()
    {
        var queue = CreateQueue();
        var fullEvents = 0;
        queue.LobbyFull += (_, _) => fullEvents++;

        await FillAsync(queue, Capacity);

        Assert.Equal(1, fullEvents);
        Assert.Equal(Capacity, queue.GetCurrentLobby().Players.Count);
    }

    [Fact]
    public async Task LeaveQueue_NotInQueue_Fails()
    {
        var queue = CreateQueue();

        var result = await queue.LeaveQueueAsync(123);

        Assert.False(result.Success);
        Assert.Contains("not in the queue", result.Message);
    }

    [Fact]
    public async Task LeaveQueue_AfterJoin_SucceedsAndRaisesPlayerLeft()
    {
        var queue = CreateQueue();
        await queue.JoinQueueAsync(1, "alice", "steam-1");
        PlayerLeftEventArgs? raised = null;
        queue.PlayerLeft += (_, e) => raised = e;

        var result = await queue.LeaveQueueAsync(1);

        Assert.True(result.Success);
        Assert.NotNull(raised);
        Assert.Null(queue.GetRegisteredPlayers().Single().CurrentLobbyId);
    }

    [Fact]
    public async Task MarkReady_NotInLobby_Fails()
    {
        var queue = CreateQueue();

        var result = await queue.MarkPlayerReadyAsync(1);

        Assert.False(result.Success);
        Assert.Contains("not in a lobby", result.Message);
    }

    [Fact]
    public async Task MarkReady_InLobby_SucceedsAndRaisesPlayerReady()
    {
        var queue = CreateQueue();
        await queue.JoinQueueAsync(1, "alice", "steam-1");
        PlayerReadyEventArgs? raised = null;
        queue.PlayerReady += (_, e) => raised = e;

        var result = await queue.MarkPlayerReadyAsync(1);

        Assert.True(result.Success);
        Assert.NotNull(raised);
        Assert.True(queue.GetRegisteredPlayers().Single().IsReady);
    }

    [Fact]
    public async Task MarkReady_Twice_SecondCallReportsAlreadyReady()
    {
        var queue = CreateQueue();
        await queue.JoinQueueAsync(1, "alice", "steam-1");
        await queue.MarkPlayerReadyAsync(1);

        var again = await queue.MarkPlayerReadyAsync(1);

        Assert.False(again.Success);
        Assert.Contains("already ready", again.Message);
    }

    [Fact]
    public async Task MarkReady_AllPlayersReady_RaisesLobbyReadyAndFlipsState()
    {
        var queue = CreateQueue();
        var ids = await FillAsync(queue, Capacity);
        var lobbyReadyCount = 0;
        queue.LobbyReady += (_, _) => lobbyReadyCount++;

        foreach (var id in ids)
        {
            await queue.MarkPlayerReadyAsync(id);
        }

        Assert.Equal(1, lobbyReadyCount);
        Assert.Equal(LobbyState.Ready, queue.GetCurrentLobby().State);
    }

    [Fact]
    public async Task MarkUnready_AfterReady_ClearsReadyAndRaisesEvent()
    {
        var queue = CreateQueue();
        await queue.JoinQueueAsync(1, "alice", "steam-1");
        await queue.MarkPlayerReadyAsync(1);
        PlayerUnreadyEventArgs? raised = null;
        queue.PlayerUnready += (_, e) => raised = e;

        var result = await queue.MarkPlayerUnreadyAsync(1);

        Assert.True(result.Success);
        Assert.NotNull(raised);
        Assert.False(queue.GetRegisteredPlayers().Single().IsReady);
    }

    [Fact]
    public async Task Kick_PlayerInLobby_RemovesAndRaisesEvent()
    {
        var queue = CreateQueue();
        await queue.JoinQueueAsync(1, "alice", "steam-1");
        PlayerKickedEventArgs? raised = null;
        queue.PlayerKicked += (_, e) => raised = e;

        var result = await queue.KickFromLobbyAsync(1);

        Assert.True(result.Success);
        Assert.NotNull(raised);
        Assert.Null(queue.GetRegisteredPlayers().Single().CurrentLobbyId);
    }

    [Fact]
    public async Task ForceLobbyReady_EmptyLobby_Fails()
    {
        var queue = CreateQueue();

        var result = await queue.ForceLobbyReadyAsync();

        Assert.False(result.Success);
        Assert.Contains("empty", result.Message);
    }

    [Fact]
    public async Task ForceLobbyReady_WithPlayers_MarksAllReadyAndRaisesLobbyReady()
    {
        var queue = CreateQueue();
        await FillAsync(queue, Capacity);
        var raised = false;
        queue.LobbyReady += (_, _) => raised = true;

        var result = await queue.ForceLobbyReadyAsync();

        Assert.True(result.Success);
        Assert.True(raised);
        Assert.All(queue.GetCurrentLobby().Players, p => Assert.True(p.IsReady));
        Assert.Equal(LobbyState.Ready, queue.GetCurrentLobby().State);
    }

    [Fact]
    public async Task ResetCurrentLobby_ClearsPlayersAndRaisesResetWithCount()
    {
        var queue = CreateQueue();
        await FillAsync(queue, Capacity);
        LobbyResetEventArgs? raised = null;
        queue.LobbyReset += (_, e) => raised = e;

        var result = await queue.ResetCurrentLobbyAsync();

        Assert.True(result.Success);
        Assert.NotNull(raised);
        Assert.Equal(Capacity, raised!.RemovedPlayerCount);
        Assert.Empty(queue.GetCurrentLobby().Players);
        Assert.All(queue.GetRegisteredPlayers(), p => Assert.Null(p.CurrentLobbyId));
    }

    [Fact]
    public async Task RemovePlayerCompletely_KnownPlayer_RemovesFromRegistry()
    {
        var queue = CreateQueue();
        await queue.JoinQueueAsync(1, "alice", "steam-1");

        Assert.True(queue.RemovePlayerCompletely(1));
        Assert.Empty(queue.GetRegisteredPlayers());
        Assert.False(queue.RemovePlayerCompletely(1)); // already gone
    }

    [Fact]
    public async Task UpdatePlayerSteamId_KnownPlayer_UpdatesValue()
    {
        var queue = CreateQueue();
        await queue.JoinQueueAsync(1, "alice", "old-steam");

        Assert.True(queue.UpdatePlayerSteamId(1, "new-steam"));
        Assert.Equal("new-steam", queue.GetRegisteredPlayers().Single().SteamId);
        Assert.False(queue.UpdatePlayerSteamId(999, "x")); // unknown user
    }

    [Fact]
    public async Task GetQueueStatus_ReflectsCurrentState()
    {
        var queue = CreateQueue();
        await queue.JoinQueueAsync(1, "alice", "steam-1");
        await queue.MarkPlayerReadyAsync(1);

        var status = queue.GetQueueStatus();

        Assert.Equal(LeagueConstants.PlayersPerLobby, status.PlayersPerLobby);
        Assert.Equal(1, status.TotalRegistered);
        Assert.Equal(1, status.CurrentLobbyPlayerCount);
        Assert.Equal(1, status.PlayersReady);
        var summary = Assert.Single(status.Players);
        Assert.Equal("alice", summary.Username);
        Assert.True(summary.IsReady);
    }

    [Fact]
    public async Task FinishLobby_SettlesLobbyAndOpensAFreshOne()
    {
        var queue = CreateQueue();
        await FillAsync(queue, Capacity);
        var originalId = queue.GetCurrentLobby().Id;

        var finishedRaised = false;
        var createdRaised = false;
        queue.LobbyFinished += (_, e) => finishedRaised = e.Lobby.Id == originalId;
        queue.LobbyCreated += (_, _) => createdRaised = true;

        await queue.FinishLobbyAsync(originalId);

        Assert.True(finishedRaised);
        Assert.True(createdRaised);
        Assert.NotEqual(originalId, queue.GetCurrentLobby().Id);
        Assert.Equal(LobbyState.Finished, queue.GetLobbyById(originalId)!.State);
    }

    [Fact]
    public async Task FinishLobby_UnknownId_IsNoOp()
    {
        var queue = CreateQueue();
        var finishedRaised = false;
        queue.LobbyFinished += (_, _) => finishedRaised = true;

        await queue.FinishLobbyAsync("does-not-exist");

        Assert.False(finishedRaised);
    }

    [Fact]
    public async Task GetStats_CountsRegisteredAndWaitingPlayers()
    {
        var queue = CreateQueue();
        await queue.JoinQueueAsync(1, "alice", "steam-1");

        var stats = queue.GetStats();

        Assert.Equal(1, stats.TotalRegisteredPlayers);
        Assert.Equal(1, stats.CurrentLobbyPlayerCount);
        Assert.Equal(0, stats.WaitingPlayers); // the only player is in the current lobby
        Assert.True(stats.TotalLobbiesCreated >= 1);
    }
}
