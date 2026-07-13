using IHLDota2Bot.Discord.Helpers;

namespace IHLDota2Bot.Discord.Tests;

public class ChannelPolicyTests
{
    [Fact]
    public void UnconfiguredChannel_AcceptsAnyChannel() =>
        Assert.True(ChannelPolicy.ShouldHandle(configuredChannelId: 0, messageChannelId: 12345));

    [Fact]
    public void ConfiguredChannel_AcceptsMatchingChannel() =>
        Assert.True(ChannelPolicy.ShouldHandle(configuredChannelId: 42, messageChannelId: 42));

    [Fact]
    public void ConfiguredChannel_RejectsOtherChannels() =>
        Assert.False(ChannelPolicy.ShouldHandle(configuredChannelId: 42, messageChannelId: 12345));
}
