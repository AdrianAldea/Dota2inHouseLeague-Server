using System.Globalization;
using Discord;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>
/// Maps a slash command's option values onto the shape the text-command handlers
/// already consume: a whitespace-token <c>Parts</c> array (command word at index 0,
/// option values after it in catalog declaration order) plus the mentioned-user list.
/// User options contribute both a mention token to <c>Parts</c> and an entry in
/// <c>MentionedUserIds</c>, exactly like an inline <c>@mention</c> in a text command,
/// so every handler works unchanged behind either front-end.
/// </summary>
public static class SlashCommandArgs
{
    public static (string[] Parts, IReadOnlyList<ulong> MentionedUserIds) Build(
        SlashCommandDefinition definition,
        IReadOnlyDictionary<string, object?> providedOptions
    )
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(providedOptions);

        var parts = new List<string> { definition.Name };
        var mentions = new List<ulong>();

        foreach (var option in definition.Options)
        {
            if (!providedOptions.TryGetValue(option.Name, out var value) || value is null)
                continue;

            switch (value)
            {
                case IUser user:
                    mentions.Add(user.Id);
                    parts.Add($"<@{user.Id}>");
                    break;
                case IFormattable formattable:
                    // Integer options arrive as long; keep numbers culture-stable.
                    parts.Add(formattable.ToString(null, CultureInfo.InvariantCulture));
                    break;
                default:
                    parts.Add(value.ToString() ?? string.Empty);
                    break;
            }
        }

        return (parts.ToArray(), mentions);
    }
}
