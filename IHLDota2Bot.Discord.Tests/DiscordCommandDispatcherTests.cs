using IHLDota2Bot.Discord.Commands;
using IHLDota2Bot.Discord.Enums;
using IHLDota2Bot.Discord.Tests.TestSupport;

namespace IHLDota2Bot.Discord.Tests;

/// <summary>
/// The dispatcher is the heart of the command-handler pipeline: it indexes the
/// registered handlers by <see cref="DiscordAction"/> and is what the router uses
/// to find the handler for an inbound command.
/// </summary>
public class DiscordCommandDispatcherTests
{
    private sealed class StubHandler(DiscordAction action, bool requiresAdmin = false)
        : IDiscordCommandHandler
    {
        public DiscordAction Action => action;
        public bool RequiresAdmin => requiresAdmin;
        public bool Invoked { get; private set; }

        public Task HandleAsync(IDiscordCommandContext context)
        {
            Invoked = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void TryGetHandler_RegisteredAction_ReturnsHandler()
    {
        var ping = new StubHandler(DiscordAction.PING);
        var dispatcher = new DiscordCommandDispatcher([ping]);

        Assert.True(dispatcher.TryGetHandler(DiscordAction.PING, out var handler));
        Assert.Same(ping, handler);
    }

    [Fact]
    public void TryGetHandler_UnregisteredAction_ReturnsFalse()
    {
        var dispatcher = new DiscordCommandDispatcher([new StubHandler(DiscordAction.PING)]);

        Assert.False(dispatcher.TryGetHandler(DiscordAction.BAN, out var handler));
        Assert.Null(handler);
    }

    [Fact]
    public void Constructor_DuplicateActions_Throws()
    {
        // Two handlers claiming the same action is a startup misconfiguration —
        // ToDictionary surfaces it loudly rather than silently dropping one.
        var handlers = new IDiscordCommandHandler[]
        {
            new StubHandler(DiscordAction.PING),
            new StubHandler(DiscordAction.PING),
        };

        Assert.Throws<ArgumentException>(() => new DiscordCommandDispatcher(handlers));
    }

    [Fact]
    public void Constructor_NullHandlers_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new DiscordCommandDispatcher(null!));

    [Fact]
    public async Task ResolvedHandler_IsInvokable()
    {
        var stub = new StubHandler(DiscordAction.STATUS);
        var dispatcher = new DiscordCommandDispatcher([stub]);

        Assert.True(dispatcher.TryGetHandler(DiscordAction.STATUS, out var handler));
        await handler.HandleAsync(new FakeCommandContext());

        Assert.True(stub.Invoked);
    }
}
