using Discord;
using Discord.WebSocket;
using IHLDota2Bot.Common.Requests;
using IHLDota2Bot.Discord.Clients;
using IHLDota2Bot.Discord.Helpers;

namespace IHLDota2Bot.Discord.Services;

public class DiscordService
{
    private bool isRunning;
    private const string DiscordToken = "MTM3MzQwMTIxMjkzNzgzMDU3Mg.GwOJf5.ZBOX0Fm6od95W77Ow7XDiZVgJ8a9Bqh4EaFiWE";
    private readonly SteamClient steamClient;
    private readonly ILogger<DiscordService> logger;
    private readonly DiscordSocketClient discordClient;

    public DiscordService(SteamClient steamClient, ILogger<DiscordService> logger)
    {
        this.steamClient = steamClient ?? throw new ArgumentNullException(nameof(steamClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        };

        discordClient = new DiscordSocketClient(config);
        discordClient.MessageReceived += HandleMessageAsync;
    }

    private async Task HandleMessageAsync(SocketMessage message)
    {
        // ensure we have a user message before touching Author
        if (message is not SocketUserMessage userMessage) return;

        if (userMessage.Author?.IsBot ?? false) return;

        var content = userMessage.Content?.Trim();
        if (string.IsNullOrEmpty(content)) return;

        if (!content.StartsWith('!'))
            return;

        var parts = content[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToString().ToUpper();

        if (Enum.TryParse(command, out Enums.DiscordAction action))
        {
            Console.WriteLine($"{nameof(HandleMessageAsync)}: Command - {action}");

            switch (action)
            {
                case Enums.DiscordAction.REGISTER:
                    await RegisterPlayerAsync(userMessage, parts);
                    break;
                case Enums.DiscordAction.PROFILE:
                    await ShowProfileAsync(userMessage);
                    break;
                case Enums.DiscordAction.CREATE_LOBBY:
                    await CreateLobbyAsync(userMessage);
                    break;
                case Enums.DiscordAction.COMMANDS:
                    await ShowCommandsAsync(userMessage);
                    break;
            }
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

        var userId = author.Id;

        var request = new RegisterPlayerRequest { SteamId = steamId, UserId = userId };
        var registeredPlayer = await steamClient.RegisterPlayerAsync(request);

        if (registeredPlayer == null)
        {
            await message.Channel.SendMessageAsync("Registration failed. Please try again later.");
            return;
        }

        var registerMessage = MessagesFormatter.FormatRegisterPlayer(
            registeredPlayer.UserId,
            registeredPlayer.SteamId
        );

        Console.WriteLine($"{nameof(RegisterPlayerAsync)}: " + message.Content);
        await message.Channel.SendMessageAsync($"You have successfully registered as {registeredPlayer.SteamId} (SteamID: {steamId})!");
    }

    private async Task ShowProfileAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var profileResponse = await steamClient.ShowProfileAsync(author.Id);
        if (profileResponse == null) return;

        var profile = MessagesFormatter.FormatProfile(
            profileResponse.Username,
            profileResponse.SteamId,
            profileResponse.Rating,
            profileResponse.Matches,
            profileResponse.Wins
        );

        Console.WriteLine($"{nameof(ShowProfileAsync)}: " + message.Content);
        await message.Channel.SendMessageAsync(profile);
    }

    private async Task CreateLobbyAsync(SocketUserMessage message)
    {
        var author = message.Author;
        if (author == null) return;

        var lobbyResponse = await steamClient.CreateLobbyAsync(author.Id);
        if (lobbyResponse == null) return;

        var createLobbyMessage = MessagesFormatter.FormatCreateLobby(
            lobbyResponse.LobbyName,
            lobbyResponse.LobbyId
        );

        await message.Channel.SendMessageAsync(createLobbyMessage);
    }

    private async Task ShowCommandsAsync(SocketUserMessage message)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting Discord service...");

        await discordClient.LoginAsync(TokenType.Bot, DiscordToken);
        await discordClient.StartAsync();

        isRunning = true;

        return isRunning;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping Discord service...");

        await discordClient.LogoutAsync();
        await discordClient.StopAsync();
        await discordClient.DisposeAsync();
    }

    public async Task SendMessageAsync(string message,CancellationToken cancellationToken)
    {
        if (discordClient.GetChannel(1373400451906666510) is IMessageChannel channel)
        {
            await channel.SendMessageAsync(message);
        }
    }
}