using System.Diagnostics.CodeAnalysis;
using IHLDota2Bot.Discord.Enums;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>
/// Routes a parsed <see cref="DiscordAction"/> to its registered
/// <see cref="IDiscordCommandHandler"/>. Commands not yet migrated to a handler
/// are absent from the registry, so <see cref="TryGetHandler"/> returns false and
/// the caller falls back to its legacy switch during the incremental migration.
/// </summary>
public sealed class DiscordCommandDispatcher
{
    private readonly IReadOnlyDictionary<DiscordAction, IDiscordCommandHandler> handlers;

    public DiscordCommandDispatcher(IEnumerable<IDiscordCommandHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        // ToDictionary throws on duplicate keys — surfaces an accidental
        // double-registration of the same action at startup rather than silently.
        this.handlers = handlers.ToDictionary(h => h.Action);
    }

    public bool TryGetHandler(
        DiscordAction action,
        [NotNullWhen(true)] out IDiscordCommandHandler? handler
    ) => handlers.TryGetValue(action, out handler);

    /// <summary>Actions with a registered handler — used to filter slash-command registration.</summary>
    public IReadOnlyCollection<DiscordAction> RegisteredActions => handlers.Keys.ToArray();
}
