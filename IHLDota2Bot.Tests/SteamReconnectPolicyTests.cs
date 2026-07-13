using IHLDota2Bot.Services;
using SteamKit2;

namespace IHLDota2Bot.Tests;

/// <summary>
/// Tests the pure reconnection policy extracted from <see cref="SteamGcClient"/>:
/// exponential backoff with a ceiling, and which logon failures are worth retrying.
/// </summary>
public class SteamReconnectPolicyTests
{
    // ── Exponential backoff: 5s doubling each attempt, capped at 2 minutes ──

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(4, 80)]
    public void NextDelay_DoublesEachAttempt_BelowTheCap(int attempt, double expectedSeconds)
    {
        Assert.Equal(expectedSeconds, SteamReconnectPolicy.NextDelay(attempt).TotalSeconds);
    }

    [Theory]
    [InlineData(5)] // 160s would exceed the cap
    [InlineData(6)]
    [InlineData(20)]
    [InlineData(1000)]
    public void NextDelay_IsCappedAtMaxDelay(int attempt)
    {
        Assert.Equal(SteamReconnectPolicy.MaxDelay, SteamReconnectPolicy.NextDelay(attempt));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void NextDelay_TreatsNegativeAttemptsAsZero(int attempt)
    {
        Assert.Equal(SteamReconnectPolicy.BaseDelay, SteamReconnectPolicy.NextDelay(attempt));
    }

    [Fact]
    public void NextDelay_NeverExceedsTheCap_AcrossManyAttempts()
    {
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var delay = SteamReconnectPolicy.NextDelay(attempt);
            Assert.True(delay >= SteamReconnectPolicy.BaseDelay);
            Assert.True(delay <= SteamReconnectPolicy.MaxDelay);
        }
    }

    // ── Non-retryable logon failures: stop reconnecting on these ──

    [Theory]
    [InlineData(EResult.InvalidPassword)]
    [InlineData(EResult.AccountLogonDenied)]
    [InlineData(EResult.AccountLoginDeniedNeedTwoFactor)]
    [InlineData(EResult.AccountDisabled)]
    [InlineData(EResult.Banned)]
    public void IsNonRetryableLogonResult_TrueForCredentialAndAccountFailures(EResult result)
    {
        Assert.True(SteamReconnectPolicy.IsNonRetryableLogonResult(result));
    }

    [Theory]
    [InlineData(EResult.OK)]
    [InlineData(EResult.ServiceUnavailable)]
    [InlineData(EResult.TryAnotherCM)]
    [InlineData(EResult.Timeout)]
    [InlineData(EResult.NoConnection)]
    public void IsNonRetryableLogonResult_FalseForTransientFailures(EResult result)
    {
        Assert.False(SteamReconnectPolicy.IsNonRetryableLogonResult(result));
    }
}
