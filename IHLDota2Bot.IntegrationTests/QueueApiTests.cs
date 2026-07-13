using System.Net.Http.Json;
using IHLDota2Bot.Common.DTOs;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.IntegrationTests.TestSupport;

namespace IHLDota2Bot.IntegrationTests;

/// <summary>
/// Queue flow over real HTTP: registration gating, join/leave, status reflection,
/// and the ban → queue-refusal interaction (DB ban state feeding the queue path).
/// </summary>
public class QueueApiTests : IntegrationTestBase
{
    private async Task RegisterAsync(ulong userId, string username) =>
        (
            await Client.PostAsJsonAsync(
                "/api/gamemanager/register",
                new RegisterPlayerRequest
                {
                    SteamId = 76561198000000000 + userId,
                    UserId = userId,
                    Username = username,
                }
            )
        ).EnsureSuccessStatusCode();

    private async Task<QueueOperationResponse> PostOp(string path) =>
        (
            await (
                await Client.PostAsync(path, content: null)
            ).Content.ReadFromJsonAsync<QueueOperationResponse>()
        )!;

    [Fact]
    public async Task Join_Unregistered_IsRefused()
    {
        var op = await PostOp("/api/gamemanager/queue/1");

        Assert.False(op.Success);
        Assert.Contains("not registered", op.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_ThenJoin_Succeeds_AndStatusReflectsIt()
    {
        await RegisterAsync(1, "alice");

        var join = await PostOp("/api/gamemanager/queue/1");
        Assert.True(join.Success);

        var status = await Client.GetFromJsonAsync<QueueStatusResponse>(
            "/api/gamemanager/queue/status"
        );
        Assert.Equal(1, status!.CurrentLobbyPlayerCount);
        Assert.Equal(1, status.TotalRegistered);
        Assert.Equal(LeagueConstants.PlayersPerLobby, status.PlayersPerLobby);
        Assert.Contains(status.Players, p => p.Username == "alice");
    }

    [Fact]
    public async Task Leave_AfterJoin_Succeeds()
    {
        await RegisterAsync(1, "alice");
        await PostOp("/api/gamemanager/queue/1");

        var leave = await PostOp("/api/gamemanager/leave/1");

        Assert.True(leave.Success);
    }

    [Fact]
    public async Task BannedPlayer_CannotJoin_UntilUnbanned()
    {
        await RegisterAsync(1, "alice");

        var ban = await PostOp("/api/gamemanager/admin/ban/1?reason=smurfing");
        Assert.True(ban.Success);

        var blocked = await PostOp("/api/gamemanager/queue/1");
        Assert.False(blocked.Success);
        Assert.Contains("banned", blocked.Message, StringComparison.OrdinalIgnoreCase);

        var unban = await PostOp("/api/gamemanager/admin/unban/1");
        Assert.True(unban.Success);

        var allowed = await PostOp("/api/gamemanager/queue/1");
        Assert.True(allowed.Success);
    }
}
