using IHLDota2Bot.Discord.Enums;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>
/// Handles a single Discord command. Implementations are registered in DI and
/// resolved by <see cref="DiscordCommandDispatcher"/> keyed on <see cref="Action"/>,
/// replacing the monolithic command switch.
/// </summary>
public interface IDiscordCommandHandler
{
    /// <summary>The command this handler is responsible for.</summary>
    DiscordAction Action { get; }

    /// <summary>
    /// When true the dispatch pipeline only runs the handler for users who pass the
    /// admin check; non-admins get a permission-denied reply. Admin handlers opt in
    /// by overriding this — the gating lives in the pipeline, not in the handler body.
    /// </summary>
    bool RequiresAdmin => false;

    Task HandleAsync(IDiscordCommandContext context);
}
