namespace IHLDota2Bot.Discord.Helpers;

/// <summary>
/// Decides whether a message's channel is served by the bot. When
/// <c>Discord:ChannelId</c> is configured, commands are only handled in that
/// channel (DMs and other channels are silently ignored) so the bot can't be
/// driven from places admins don't watch. An unconfigured id (0) accepts
/// commands anywhere — the dev-friendly default. Pure and static, mirroring
/// <see cref="AdminPolicy"/>, so the rule is unit-testable without the gateway.
/// </summary>
public static class ChannelPolicy
{
    public static bool ShouldHandle(ulong configuredChannelId, ulong messageChannelId) =>
        configuredChannelId == 0 || configuredChannelId == messageChannelId;
}
