using System.Text.RegularExpressions;
using Discord;
using IHLDota2Bot.Discord.Commands;
using IHLDota2Bot.Discord.Enums;

namespace IHLDota2Bot.Discord.Tests;

/// <summary>
/// The slash-command catalog is what the bot registers with Discord on startup.
/// These tests pin the Discord API's structural rules (name shape, description
/// length, required-before-optional ordering) so a bad entry fails in CI instead
/// of as a registration error at runtime, and pin the catalog's one-to-one
/// mapping onto <see cref="DiscordAction"/>.
/// </summary>
public class SlashCommandCatalogTests
{
    // Discord's rules for chat-input command and option names (ASCII subset:
    // lowercase, digits, - and _, 1-32 chars).
    private static readonly Regex ValidName = new("^[a-z0-9_-]{1,32}$", RegexOptions.Compiled);

    [Fact]
    public void Definitions_CoverEveryActionExceptTextAliases()
    {
        var covered = SlashCommandCatalog.Definitions.Select(d => d.Action).ToHashSet();

        // HELP exists only as the text alias `!help` → COMMANDS; the slash
        // front-end registers a single /help mapped to COMMANDS instead.
        var expected = Enum.GetValues<DiscordAction>()
            .Where(a => a != DiscordAction.HELP)
            .ToHashSet();

        Assert.Equal(expected, covered);
    }

    [Fact]
    public void Definitions_ActionsAndNamesAreUnique()
    {
        Assert.Distinct(SlashCommandCatalog.Definitions.Select(d => d.Action));
        Assert.Distinct(SlashCommandCatalog.Definitions.Select(d => d.Name));
    }

    [Fact]
    public void Definitions_NamesAndDescriptionsSatisfyDiscordLimits()
    {
        foreach (var definition in SlashCommandCatalog.Definitions)
        {
            Assert.Matches(ValidName, definition.Name);
            Assert.InRange(definition.Description.Length, 1, 100);

            foreach (var option in definition.Options)
            {
                Assert.Matches(ValidName, option.Name);
                Assert.InRange(option.Description.Length, 1, 100);
            }
        }
    }

    [Fact]
    public void Definitions_RequiredOptionsPrecedeOptionalOnes()
    {
        foreach (var definition in SlashCommandCatalog.Definitions)
        {
            var seenOptional = false;
            foreach (var option in definition.Options)
            {
                if (!option.Required)
                    seenOptional = true;
                else
                    Assert.False(
                        seenOptional,
                        $"/{definition.Name}: required option '{option.Name}' after an optional one"
                    );
            }
        }
    }

    [Fact]
    public void BuildProperties_BuildsEveryDefinition()
    {
        // SlashCommandBuilder applies Discord.Net's own validation on Build().
        var properties = SlashCommandCatalog.BuildProperties();

        Assert.Equal(SlashCommandCatalog.Definitions.Count, properties.Length);
    }

    [Fact]
    public void TryGetByName_KnownAndUnknownNames()
    {
        Assert.True(SlashCommandCatalog.TryGetByName("queue", out var queue));
        Assert.Equal(DiscordAction.QUEUE, queue.Action);

        Assert.False(SlashCommandCatalog.TryGetByName("does_not_exist", out _));
    }

    [Fact]
    public void Report_ConstrainsWinnerToRadiantOrDire()
    {
        Assert.True(SlashCommandCatalog.TryGetByName("report", out var report));

        var winner = Assert.Single(report.Options);
        Assert.True(winner.Required);
        Assert.Equal(["radiant", "dire"], winner.Choices);
    }

    [Fact]
    public void AdminCommands_MatchTheTextFrontEndSet()
    {
        // Admin gating itself lives on the handlers (RequiresAdmin) and is enforced
        // by the pipeline for both front-ends; this pins that the catalog exposes
        // the admin commands under their expected names.
        string[] adminNames =
        [
            "ban",
            "unban",
            "kick",
            "force_start",
            "reset_queue",
            "add_account",
            "pool_status",
        ];

        foreach (var name in adminNames)
            Assert.True(SlashCommandCatalog.TryGetByName(name, out _), $"missing /{name}");
    }

    [Fact]
    public void UserOptions_UseTheUserOptionType()
    {
        foreach (var definition in SlashCommandCatalog.Definitions)
        foreach (var option in definition.Options.Where(o => o.Name == "player"))
            Assert.Equal(ApplicationCommandOptionType.User, option.Type);
    }
}
