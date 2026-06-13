using IHLDota2Bot;
using Microsoft.Extensions.Logging;
using Moq;

namespace IHLDota2Bot.Tests;

/// <summary>
/// Tests the bookkeeping pool that hands Steam accounts out to lobbies. Pure
/// domain — the only collaborator is an <see cref="ILogger{T}"/> (Moq stub).
/// </summary>
public class SteamAccountPoolTests
{
    private static SteamAccountPool CreatePool() => new(Mock.Of<ILogger<SteamAccountPool>>());

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SteamAccountPool(null!));
    }

    [Fact]
    public async Task RegisterAccount_NewAccount_AddsItAsReady()
    {
        var pool = CreatePool();

        await pool.RegisterAccountAsync("bot1", "pw");

        var account = pool.GetAccountByUsername("bot1");
        Assert.NotNull(account);
        Assert.Equal(AccountState.Ready, account!.State);
        Assert.True(pool.HasAvailableAccounts());
        Assert.Equal(1, pool.GetAvailableAccountCount());
    }

    [Fact]
    public async Task RegisterAccount_DuplicateUsername_IsIgnored()
    {
        var pool = CreatePool();
        await pool.RegisterAccountAsync("bot1", "pw");

        await pool.RegisterAccountAsync("bot1", "different-pw");

        Assert.Single(pool.GetAllAccounts());
        Assert.Equal("pw", pool.GetAccountByUsername("bot1")!.Password); // original kept
    }

    [Fact]
    public async Task GetAvailableAccount_ReservesAndFlipsToInUse()
    {
        var pool = CreatePool();
        await pool.RegisterAccountAsync("bot1", "pw");

        var reserved = await pool.GetAvailableAccountAsync();

        Assert.NotNull(reserved);
        Assert.Equal(AccountState.InUse, reserved!.State);
        Assert.False(pool.HasAvailableAccounts()); // the only account is now in use
    }

    [Fact]
    public async Task GetAvailableAccount_NoneReady_ReturnsNull()
    {
        var pool = CreatePool();

        var reserved = await pool.GetAvailableAccountAsync();

        Assert.Null(reserved);
    }

    [Fact]
    public async Task GetAvailableAccount_HandsOutEachReadyAccountExactlyOnce()
    {
        var pool = CreatePool();
        await pool.RegisterAccountAsync("bot1", "pw");
        await pool.RegisterAccountAsync("bot2", "pw");

        var first = await pool.GetAvailableAccountAsync();
        var second = await pool.GetAvailableAccountAsync();
        var third = await pool.GetAvailableAccountAsync();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first!.Id, second!.Id);
        Assert.Null(third); // pool exhausted
    }

    [Fact]
    public async Task MarkAccountInUse_SetsStateAndLobby()
    {
        var pool = CreatePool();
        await pool.RegisterAccountAsync("bot1", "pw");
        var account = pool.GetAccountByUsername("bot1")!;

        await pool.MarkAccountInUseAsync(account.Id, "lobby-7");

        Assert.Equal(AccountState.InUse, account.State);
        Assert.Equal("lobby-7", account.CurrentLobbyId);
        Assert.NotNull(account.LastUsedAt);
    }

    [Fact]
    public async Task ReleaseAccount_ReturnsItToReadyAndClearsLobby()
    {
        var pool = CreatePool();
        await pool.RegisterAccountAsync("bot1", "pw");
        var account = await pool.GetAvailableAccountAsync(); // now InUse
        await pool.MarkAccountInUseAsync(account!.Id, "lobby-7");

        await pool.ReleaseAccountAsync(account.Id);

        Assert.Equal(AccountState.Ready, account.State);
        Assert.Null(account.CurrentLobbyId);
        Assert.NotNull(account.LastUsedAt);
        Assert.True(pool.HasAvailableAccounts()); // available again
    }

    [Fact]
    public async Task ReleaseAccount_UnknownId_IsNoOp()
    {
        var pool = CreatePool();
        await pool.RegisterAccountAsync("bot1", "pw");

        await pool.ReleaseAccountAsync("not-a-real-id");

        Assert.Equal(1, pool.GetAvailableAccountCount());
    }

    [Fact]
    public async Task GetStats_ReportsCountsByState()
    {
        var pool = CreatePool();
        await pool.RegisterAccountAsync("ready1", "pw");
        await pool.RegisterAccountAsync("ready2", "pw");
        await pool.RegisterAccountAsync("busy", "pw");
        await pool.GetAvailableAccountAsync(); // reserves one (state order is insertion order)

        var stats = pool.GetStats();

        Assert.Equal(3, stats.TotalAccounts);
        Assert.Equal(1, stats.InUseAccounts);
        Assert.Equal(2, stats.ReadyAccounts);
        Assert.Equal(0, stats.OnCooldownAccounts);
    }

    [Fact]
    public async Task GetAccountById_ReturnsRegisteredAccount()
    {
        var pool = CreatePool();
        await pool.RegisterAccountAsync("bot1", "pw");
        var byName = pool.GetAccountByUsername("bot1")!;

        var byId = pool.GetAccountById(byName.Id);

        Assert.Same(byName, byId);
        Assert.Null(pool.GetAccountById("missing"));
    }
}
