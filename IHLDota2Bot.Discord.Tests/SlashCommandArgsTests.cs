using Discord;
using IHLDota2Bot.Discord.Commands;
using Moq;

namespace IHLDota2Bot.Discord.Tests;

/// <summary>
/// <see cref="SlashCommandArgs"/> is the bridge between slash-command options and
/// the text-command <c>Parts</c>/mentions shape the handlers parse positionally.
/// These tests pin that mapping per argument kind so a catalog reordering or a
/// type change can't silently shift what a handler reads at Parts[1].
/// </summary>
public class SlashCommandArgsTests
{
    private static IUser UserWithId(ulong id)
    {
        var user = new Mock<IUser>();
        user.SetupGet(u => u.Id).Returns(id);
        return user.Object;
    }

    private static SlashCommandDefinition Definition(string name) =>
        SlashCommandCatalog.TryGetByName(name, out var definition)
            ? definition
            : throw new ArgumentException($"unknown command {name}");

    [Fact]
    public void Build_NoOptions_YieldsCommandWordOnly()
    {
        var (parts, mentions) = SlashCommandArgs.Build(
            Definition("queue"),
            new Dictionary<string, object?>()
        );

        Assert.Equal(["queue"], parts);
        Assert.Empty(mentions);
    }

    [Fact]
    public void Build_StringOption_LandsAtParts1()
    {
        // RegisterCommandHandler parses Parts[1] as the Steam ID.
        var (parts, _) = SlashCommandArgs.Build(
            Definition("register"),
            new Dictionary<string, object?> { ["steamid"] = "76561198000000001" }
        );

        Assert.Equal(["register", "76561198000000001"], parts);
    }

    [Fact]
    public void Build_IntegerOption_IsInvariantCultureFormatted()
    {
        // Discord.Net delivers Integer option values as long.
        var (parts, _) = SlashCommandArgs.Build(
            Definition("leaderboard"),
            new Dictionary<string, object?> { ["count"] = 25L }
        );

        Assert.Equal(["leaderboard", "25"], parts);
    }

    [Fact]
    public void Build_UserOption_AddsMentionTokenAndMentionId()
    {
        var (parts, mentions) = SlashCommandArgs.Build(
            Definition("stats"),
            new Dictionary<string, object?> { ["player"] = UserWithId(42) }
        );

        Assert.Equal(["stats", "<@42>"], parts);
        Assert.Equal([42UL], mentions);
    }

    [Fact]
    public void Build_BanWithReason_KeepsTextCommandShape()
    {
        // BanCommandHandler reads the target from mentions and the reason from
        // Parts[2..] — the mention token at Parts[1] preserves those positions.
        var (parts, mentions) = SlashCommandArgs.Build(
            Definition("ban"),
            new Dictionary<string, object?>
            {
                ["player"] = UserWithId(7),
                ["reason"] = "toxic behavior",
            }
        );

        Assert.Equal(["ban", "<@7>", "toxic behavior"], parts);
        Assert.Equal([7UL], mentions);
        Assert.Equal("toxic behavior", string.Join(' ', parts.Skip(2)));
    }

    [Fact]
    public void Build_MatchesWithCountAndPlayer_CountStaysAtParts1()
    {
        // RecentMatchesCommandHandler int-parses Parts[1]; catalog order (count
        // before player) keeps the count there when both options are provided.
        var (parts, mentions) = SlashCommandArgs.Build(
            Definition("matches"),
            new Dictionary<string, object?> { ["count"] = 10L, ["player"] = UserWithId(9) }
        );

        Assert.Equal(["matches", "10", "<@9>"], parts);
        Assert.Equal([9UL], mentions);
    }

    [Fact]
    public void Build_OmittedOptionalOptions_AreSkipped()
    {
        var (parts, mentions) = SlashCommandArgs.Build(
            Definition("matches"),
            new Dictionary<string, object?> { ["player"] = UserWithId(9) }
        );

        Assert.Equal(["matches", "<@9>"], parts);
        Assert.Equal([9UL], mentions);
    }

    [Fact]
    public void Build_AddAccount_UsernameAndPasswordPositional()
    {
        // Placeholder value only — a realistic-looking one trips secret scanners.
        var (parts, _) = SlashCommandArgs.Build(
            Definition("add_account"),
            new Dictionary<string, object?>
            {
                ["username"] = "botuser",
                ["password"] = "placeholder-password",
            }
        );

        Assert.Equal(["add_account", "botuser", "placeholder-password"], parts);
    }
}
