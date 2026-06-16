using IHLDota2Bot.Discord.Enums;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>
/// Handles a single Discord command. Implementations are registered in DI and
/// resolved by <see cref="DiscordCommandDispatcher"/> keyed on <see cref="Action"/>,
/// replacing the monolithic command switch one command at a time.
/// </summary>
public interface IDiscordCommandHandler
{
    DiscordAction Action { get; }
    Task HandleAsync(DiscordCommandContext context);
}
