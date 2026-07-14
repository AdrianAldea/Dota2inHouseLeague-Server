namespace IHLDota2Bot.Tests;

/// <summary>
/// The launch path and the /me/match endpoint both derive the Dota lobby
/// name/password from the lobby id — these pin the derivation so the two can
/// never disagree silently.
/// </summary>
public class LobbyNamingTests
{
    [Fact]
    public void For_LongLobbyId_UsesFirstEightChars() =>
        Assert.Equal("Dota2IHL-abcdef12", LobbyNaming.For("abcdef1234567890"));

    [Fact]
    public void For_ShortLobbyId_UsesTheWholeId() =>
        Assert.Equal("Dota2IHL-ab12", LobbyNaming.For("ab12"));

    [Fact]
    public void For_IsDeterministic() =>
        Assert.Equal(LobbyNaming.For("deadbeef-1"), LobbyNaming.For("deadbeef-1"));

    [Fact]
    public void For_NullLobbyId_Throws() =>
        Assert.Throws<ArgumentNullException>(() => LobbyNaming.For(null!));
}
