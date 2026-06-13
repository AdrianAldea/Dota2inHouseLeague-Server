using IHLDota2Bot.Common.DTOs;

namespace IHLDota2Bot.Common.Tests;

/// <summary>
/// These enums travel over HTTP (serialized as integers by System.Text.Json) and
/// are produced by casting server-side enums to their Common counterparts
/// (e.g. <c>(PoolAccountState)account.State</c>). Their numeric values are therefore
/// a wire contract: reordering a member would silently misinterpret persisted/in-flight
/// data. These tests fail loudly if anyone renumbers them.
/// </summary>
public class EnumWireContractTests
{
    [Fact]
    public void MatchTeam_HasStableValues()
    {
        Assert.Equal(0, (int)MatchTeam.Radiant);
        Assert.Equal(1, (int)MatchTeam.Dire);
    }

    [Fact]
    public void MatchStatus_HasStableValues()
    {
        Assert.Equal(0, (int)MatchStatus.InProgress);
        Assert.Equal(1, (int)MatchStatus.Completed);
        Assert.Equal(2, (int)MatchStatus.Cancelled);
    }

    [Fact]
    public void ChallengeStatus_HasStableValues()
    {
        Assert.Equal(0, (int)ChallengeStatus.Pending);
        Assert.Equal(1, (int)ChallengeStatus.Accepted);
        Assert.Equal(2, (int)ChallengeStatus.Declined);
        Assert.Equal(3, (int)ChallengeStatus.Expired);
        Assert.Equal(4, (int)ChallengeStatus.Cancelled);
    }

    [Fact]
    public void PoolAccountState_HasStableValues()
    {
        Assert.Equal(0, (int)PoolAccountState.Ready);
        Assert.Equal(1, (int)PoolAccountState.InUse);
        Assert.Equal(2, (int)PoolAccountState.Cooldown);
    }

    [Fact]
    public void PoolSessionState_HasStableValues()
    {
        Assert.Equal(0, (int)PoolSessionState.Ready);
        Assert.Equal(1, (int)PoolSessionState.InProgress);
        Assert.Equal(2, (int)PoolSessionState.Finished);
    }
}
