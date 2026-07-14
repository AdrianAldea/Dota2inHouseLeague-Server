using Discord;
using IHLDota2Bot.Discord.Enums;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>
/// A single slash-command definition: the Discord-facing name/description/options
/// and the <see cref="DiscordAction"/> it maps to. Option order matters — provided
/// option values are appended to <see cref="IDiscordCommandContext.Parts"/> in
/// declaration order so the positional parsing in the existing handlers keeps
/// working unchanged (see <see cref="SlashCommandArgs"/>).
/// </summary>
public sealed record SlashCommandDefinition(
    DiscordAction Action,
    string Name,
    string Description,
    params SlashCommandOptionDefinition[] Options
);

/// <summary>One option of a slash command. <paramref name="Choices"/> restricts a string option to fixed values.</summary>
public sealed record SlashCommandOptionDefinition(
    string Name,
    string Description,
    ApplicationCommandOptionType Type,
    bool Required,
    params string[] Choices
);

/// <summary>
/// The full set of slash commands the bot registers, mirroring the `!`-prefix
/// commands one-to-one. Both front-ends dispatch to the same
/// <see cref="IDiscordCommandHandler"/> per action, so behavior stays identical;
/// slash commands add discoverability and client-side argument validation.
/// </summary>
public static class SlashCommandCatalog
{
    private const ApplicationCommandOptionType UserOpt = ApplicationCommandOptionType.User;
    private const ApplicationCommandOptionType StringOpt = ApplicationCommandOptionType.String;
    private const ApplicationCommandOptionType IntOpt = ApplicationCommandOptionType.Integer;

    public static readonly IReadOnlyList<SlashCommandDefinition> Definitions =
    [
        // Registration
        new(
            DiscordAction.REGISTER,
            "register",
            "Link your Discord account to a 17-digit Steam ID",
            new SlashCommandOptionDefinition("steamid", "Your 17-digit Steam ID", StringOpt, true)
        ),
        new(
            DiscordAction.LINK,
            "link",
            "Update the Steam ID on your existing registration",
            new SlashCommandOptionDefinition("steamid", "Your 17-digit Steam ID", StringOpt, true)
        ),
        new(DiscordAction.UNREGISTER, "unregister", "Leave the league and the current queue"),
        // Queue / lobby
        new(DiscordAction.QUEUE, "queue", "Join the matchmaking queue"),
        new(DiscordAction.LEAVE, "leave", "Leave the current lobby/queue"),
        new(DiscordAction.READY, "ready", "Mark yourself ready in the current lobby"),
        new(DiscordAction.UNREADY, "unready", "Undo your ready state"),
        new(DiscordAction.STATUS, "status", "Show the current queue/lobby state"),
        // Queries
        new(DiscordAction.PROFILE, "profile", "Show your profile (rating and W/L)"),
        new(DiscordAction.MYSTATS, "mystats", "Your rating, W-L, and win-rate"),
        new(
            DiscordAction.STATS,
            "stats",
            "Stats for a player (defaults to you)",
            new SlashCommandOptionDefinition("player", "Player to look up", UserOpt, false)
        ),
        new(
            DiscordAction.LEADERBOARD,
            "leaderboard",
            "Top players by rating (default 10, max 50)",
            new SlashCommandOptionDefinition("count", "How many rows to show", IntOpt, false)
        ),
        new(
            DiscordAction.RANK,
            "rank",
            "Leaderboard rank and percentile (defaults to you)",
            new SlashCommandOptionDefinition("player", "Player to look up", UserOpt, false)
        ),
        new(DiscordAction.MATCH, "match", "Show your current in-progress match"),
        new(
            DiscordAction.MATCHES,
            "matches",
            "Recent completed matches (default 5, max 20)",
            new SlashCommandOptionDefinition("count", "How many matches to show", IntOpt, false),
            new SlashCommandOptionDefinition("player", "Player to look up", UserOpt, false)
        ),
        new(
            DiscordAction.SCORE,
            "score",
            "Show a finished match by its match ID",
            new SlashCommandOptionDefinition("matchid", "Match ID (UUID)", StringOpt, true)
        ),
        // Match actions
        new(
            DiscordAction.REPORT,
            "report",
            "Report the winner of your current match",
            new SlashCommandOptionDefinition(
                "winner",
                "The winning side",
                StringOpt,
                true,
                "radiant",
                "dire"
            )
        ),
        new(
            DiscordAction.CHALLENGE,
            "challenge",
            "Send a 1v1 duel invite (expires in 15 minutes)",
            new SlashCommandOptionDefinition("player", "Player to challenge", UserOpt, true)
        ),
        new(DiscordAction.ACCEPT_MATCH, "accept_match", "Accept your latest pending challenge"),
        new(DiscordAction.DECLINE_MATCH, "decline_match", "Decline your latest pending challenge"),
        // Info
        new(DiscordAction.PING, "ping", "Bot health and gateway latency"),
        new(DiscordAction.VERSION, "version", "Bot version and build sha"),
        new(
            DiscordAction.COMMANDS,
            "help",
            "List every command, or show details for one",
            new SlashCommandOptionDefinition(
                "command",
                "Command to show details for",
                StringOpt,
                false
            )
        ),
        // Admin
        new(
            DiscordAction.BAN,
            "ban",
            "Ban a player from queueing (admin)",
            new SlashCommandOptionDefinition("player", "Player to ban", UserOpt, true),
            new SlashCommandOptionDefinition("reason", "Why the ban was issued", StringOpt, false)
        ),
        new(
            DiscordAction.UNBAN,
            "unban",
            "Clear a player's ban (admin)",
            new SlashCommandOptionDefinition("player", "Player to unban", UserOpt, true)
        ),
        new(
            DiscordAction.KICK,
            "kick",
            "Remove a player from their current lobby (admin)",
            new SlashCommandOptionDefinition("player", "Player to kick", UserOpt, true)
        ),
        new(
            DiscordAction.FORCE_START,
            "force_start",
            "Mark the current lobby ready and create a match (admin)"
        ),
        new(
            DiscordAction.RESET_QUEUE,
            "reset_queue",
            "Empty the current lobby and release players (admin)"
        ),
        new(
            DiscordAction.ADD_ACCOUNT,
            "add_account",
            "Add a Steam account to the bot pool (admin)",
            new SlashCommandOptionDefinition("username", "Steam login username", StringOpt, true),
            new SlashCommandOptionDefinition("password", "Steam login password", StringOpt, true)
        ),
        new(
            DiscordAction.POOL_STATUS,
            "pool_status",
            "List every Steam pool account and its state (admin)"
        ),
    ];

    private static readonly IReadOnlyDictionary<string, SlashCommandDefinition> ByName =
        Definitions.ToDictionary(d => d.Name, StringComparer.Ordinal);

    public static bool TryGetByName(string name, out SlashCommandDefinition definition) =>
        ByName.TryGetValue(name, out definition!);

    /// <summary>Builds the Discord.Net registration payload for every definition.</summary>
    public static ApplicationCommandProperties[] BuildProperties() =>
        Definitions.Select(BuildCommand).ToArray<ApplicationCommandProperties>();

    /// <summary>Builds the Discord.Net registration payload for one definition.</summary>
    public static SlashCommandProperties BuildCommand(SlashCommandDefinition definition)
    {
        var builder = new SlashCommandBuilder()
            .WithName(definition.Name)
            .WithDescription(definition.Description);

        foreach (var option in definition.Options)
        {
            var optionBuilder = new SlashCommandOptionBuilder()
                .WithName(option.Name)
                .WithDescription(option.Description)
                .WithType(option.Type)
                .WithRequired(option.Required);

            foreach (var choice in option.Choices)
                optionBuilder.AddChoice(choice, choice);

            builder.AddOption(optionBuilder);
        }

        return builder.Build();
    }
}
