using System.Diagnostics;
using Discord;
using Discord.WebSocket;
using IHLDota2Bot.Discord.Commands;
using IHLDota2Bot.Discord.Configuration;
using IHLDota2Bot.Discord.Enums;
using IHLDota2Bot.Discord.Helpers;
using IHLDota2Bot.Discord.Telemetry;
using Microsoft.Extensions.Options;

namespace IHLDota2Bot.Discord.Services;

/// <summary>
/// Hosts the Discord gateway connection and routes inbound `!`-prefixed messages.
/// The routing pipeline is intentionally thin: parse the command, resolve its
/// <see cref="IDiscordCommandHandler"/> via the <see cref="DiscordCommandDispatcher"/>,
/// enforce admin gating from the handler's metadata, then invoke it. All command
/// behavior lives in the individual handlers.
/// </summary>
public class DiscordService : IHostedService, IAsyncDisposable
{
    private readonly DiscordCommandDispatcher dispatcher;
    private readonly CommandRateLimiter rateLimiter;
    private readonly DiscordCommandTelemetry telemetry;
    private readonly ILogger<DiscordService> logger;
    private readonly DiscordOptions options;
    private readonly DiscordSocketClient discordClient;
    private bool isRunning;

    public DiscordService(
        DiscordCommandDispatcher dispatcher,
        CommandRateLimiter rateLimiter,
        DiscordCommandTelemetry telemetry,
        DiscordSocketClient discordClient,
        IOptions<DiscordOptions> options,
        ILogger<DiscordService> logger
    )
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(telemetry);
        ArgumentNullException.ThrowIfNull(discordClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.dispatcher = dispatcher;
        this.rateLimiter = rateLimiter;
        this.telemetry = telemetry;
        this.discordClient = discordClient;
        this.options = options.Value;
        this.logger = logger;

        discordClient.MessageReceived += HandleMessageAsync;
        discordClient.Log += OnDiscordLogAsync;
    }

    private Task OnDiscordLogAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information,
        };
        logger.Log(
            level,
            message.Exception,
            "[Discord] {Source}: {Message}",
            message.Source,
            message.Message
        );
        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage)
            return;
        if (userMessage.Author?.IsBot ?? false)
            return;

        var content = userMessage.Content?.Trim();
        if (string.IsNullOrEmpty(content))
            return;
        if (!content.StartsWith('!'))
            return;

        // Serve only the configured league channel (unset = anywhere, for dev).
        if (!ChannelPolicy.ShouldHandle(options.ChannelId, userMessage.Channel.Id))
        {
            logger.LogDebug(
                "Ignoring command in channel {ChannelId} (configured channel is {ConfiguredChannelId})",
                userMessage.Channel.Id,
                options.ChannelId
            );
            return;
        }

        var parts = content[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        var rawCommand = parts[0].ToUpperInvariant();
        var command = rawCommand switch
        {
            "Q" => "QUEUE",
            "HELP" => "COMMANDS",
            _ => rawCommand,
        };

        if (!Enum.TryParse(command, out DiscordAction action))
        {
            logger.LogDebug("Unknown command received: {Command}", rawCommand);
            return;
        }

        if (!dispatcher.TryGetHandler(action, out var handler))
        {
            logger.LogDebug("No handler registered for command: {Action}", action);
            return;
        }

        if (handler.RequiresAdmin && !IsAdmin(userMessage))
        {
            telemetry.Record(action.ToString(), DiscordCommandTelemetry.OutcomeDenied, 0);
            await userMessage.Channel.SendMessageAsync(CommandMessages.NotAuthorized);
            return;
        }

        // Throttle ordinary users to curb spam. Admin commands bypass the limit so
        // moderation isn't blocked when the channel is busy.
        if (
            !handler.RequiresAdmin
            && userMessage.Author is { } author
            && !rateLimiter.TryAcquire(author.Id, out var retryAfter)
        )
        {
            telemetry.Record(action.ToString(), DiscordCommandTelemetry.OutcomeRateLimited, 0);
            await userMessage.Channel.SendMessageAsync(
                $"Slow down — try again in {Math.Ceiling(retryAfter.TotalSeconds)}s."
            );
            return;
        }

        logger.LogInformation("Received command: {Action}", action);

        using var activity = DiscordCommandTelemetry.ActivitySource.StartActivity(
            "discord.command.handle"
        );
        activity?.SetTag("discord.command", action.ToString());

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await handler.HandleAsync(
                new DiscordCommandContext { Message = userMessage, Parts = parts }
            );
            telemetry.Record(
                action.ToString(),
                DiscordCommandTelemetry.OutcomeHandled,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            telemetry.Record(
                action.ToString(),
                DiscordCommandTelemetry.OutcomeFailed,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds
            );
            logger.LogError(ex, "Command handler for {Action} threw", action);
        }
    }

    private bool IsAdmin(SocketUserMessage message)
    {
        var author = message.Author;
        var roleNames = author is SocketGuildUser guildUser
            ? guildUser.Roles.Select(r => r.Name)
            : Enumerable.Empty<string>();

        return AdminPolicy.IsAdmin(author?.Id, roleNames, options);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (isRunning)
            return;

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            logger.LogError("Discord token is not configured. Set Discord:Token in user-secrets.");
            return;
        }

        logger.LogInformation("Starting Discord service...");
        await discordClient.LoginAsync(TokenType.Bot, options.Token);
        await discordClient.StartAsync();
        isRunning = true;
    }

    public bool IsRunning => isRunning;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!isRunning)
            return;

        logger.LogInformation("Stopping Discord service...");
        await discordClient.LogoutAsync();
        await discordClient.StopAsync();
        isRunning = false;
    }

    public async Task SendMessageAsync(
        string message,
        CancellationToken cancellationToken = default
    )
    {
        if (options.ChannelId == 0)
        {
            logger.LogWarning("Discord channel id is not configured; dropping message");
            return;
        }

        if (discordClient.GetChannel(options.ChannelId) is IMessageChannel channel)
        {
            await channel.SendMessageAsync(message);
        }
        else
        {
            logger.LogWarning("Discord channel {ChannelId} not found", options.ChannelId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await discordClient.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
