using Discord;
using Discord.WebSocket;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.Discord.Configuration;
using IHLDota2Bot.Discord.Enums;
using IHLDota2Bot.Discord.Helpers;
using Microsoft.Extensions.Options;

namespace IHLDota2Bot.Discord.Services;

public class DiscordService : IAsyncDisposable
{
    private readonly SteamClient steamClient;
    private readonly ILogger<DiscordService> logger;
    private readonly DiscordOptions options;
    private readonly DiscordSocketClient discordClient;
    private bool isRunning;

    public DiscordService(
        SteamClient steamClient,
        IOptions<DiscordOptions> options,
        ILogger<DiscordService> logger)
    {
        ArgumentNullException.ThrowIfNull(steamClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.steamClient = steamClient;
        this.options = options.Value;
        this.logger = logger;

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        };

        discordClient = new DiscordSocketClient(config);
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
            _ => LogLevel.Information
        };
        logger.Log(level, message.Exception, "[Discord] {Source}: {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage) return;
        if (userMessage.Author?.IsBot ?? false) return;

        var content = userMessage.Content?.Trim();
        if (string.IsNullOrEmpty(content)) return;
        if (!content.StartsWith('!')) return;

        var parts = content[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var command = parts[0].ToUpperInvariant();
        if (!Enum.TryParse(command, out DiscordAction action))
        {
            logger.LogDebug("Unknown command received: {Command}", command);
            return;
        }

        logger.LogInformation("Received command: {Action}", action);

        switch (action)
        {
            case DiscordAction.REGISTER:
                await RegisterPlayerAsync(userMessage, parts);
                break;
            case DiscordAction.PROFILE:
                await ShowProfileAsync(userMessage);
                break;
            case DiscordAction.CREATE_LOBBY:
                await CreateLobbyAsync(userMessage);
                break;
            case DiscordAction.COMMANDS:
                await ShowCommandsAsync(userMessage);
                break;
        }
    }

    private async Task RegisterPlayerAsync(SocketUserMessage message, string[] parts)
    {
        if (parts.Length < 2)
        {
            await message.Channel.SendMessageAsync("Usage: !register <steamId>");
            return;
        }

        if (!ulong.TryParse(parts[1], out var steamId))
        {
            await message.Channel.SendMessageAsync("Invalid SteamID format.");
            return;
        }

        var author = message.Author;
        if (author == null) return;

        var request = new RegisterPlayerRequest
        {
            SteamId = steamId,
            UserId = author.Id,
            Username = author.Username
        };
        var registeredPlayer = await steamClient.RegisterPlayerAsync(request);

        if (registeredPlayer == null)
        {
            await message.Channel.SendMessageAsync("Registration failed. Please try again later.");
            return;
        }

        await message.Channel.SendMessageAsync(
            $"You have successfully registered as {registeredPlayer.Username} (SteamID: {registeredPlayer.SteamId})!");
    }

    private async Task ShowProfileAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var profileResponse = await steamClient.ShowProfileAsync(author.Id);
        if (profileResponse == null)
        {
            await message.Channel.SendMessageAsync("Profile not found.");
            return;
        }

        var profile = MessagesFormatter.FormatProfile(
            profileResponse.Username,
            profileResponse.SteamId,
            profileResponse.Rating,
            profileResponse.Matches,
            profileResponse.Wins);

        await message.Channel.SendMessageAsync(profile);
    }

    private async Task CreateLobbyAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var lobbyResponse = await steamClient.CreateLobbyAsync(author.Id);
        if (lobbyResponse == null)
        {
            await message.Channel.SendMessageAsync("Failed to create lobby.");
            return;
        }

        var createLobbyMessage = MessagesFormatter.FormatCreateLobby(
            lobbyResponse.LobbyName,
            lobbyResponse.LobbyId);

        await message.Channel.SendMessageAsync(createLobbyMessage);
    }

    private static async Task ShowCommandsAsync(SocketUserMessage message)
    {
        var commands = string.Join("\n", new[]
        {
            "**Available commands**",
            "`!register <steamId>` — register your Steam account",
            "`!profile` — show your profile",
            "`!create_lobby` — create a new lobby",
            "`!commands` — show this list"
        });
        await message.Channel.SendMessageAsync(commands);
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken)
    {
        if (isRunning) return true;

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            logger.LogError("Discord token is not configured. Set Discord:Token in user-secrets.");
            return false;
        }

        logger.LogInformation("Starting Discord service...");
        await discordClient.LoginAsync(TokenType.Bot, options.Token);
        await discordClient.StartAsync();
        isRunning = true;
        return true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!isRunning) return;

        logger.LogInformation("Stopping Discord service...");
        await discordClient.LogoutAsync();
        await discordClient.StopAsync();
        isRunning = false;
    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
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
