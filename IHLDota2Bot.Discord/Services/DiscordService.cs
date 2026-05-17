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
        if (message.Author.IsBot) return;

        var userMessage = message as SocketUserMessage;
        if (userMessage == null) return;

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
                    await RegisterPlayerAsync(message, parts);
                    break;
                case Enums.DiscordAction.PROFILE:
                    await ShowProfileAsync(message);
                    break;
                case Enums.DiscordAction.CREATE_LOBBY:
                    await CreateLobbyAsync(message);
                    break;
                case Enums.DiscordAction.COMMANDS:
                    await ShowCommandsAsync(message);
                    break;
            }
        }
    }

    private async Task RegisterPlayerAsync(SocketMessage message, string[] parts)
    {
        //var steamIdOptional = StringSteamHelper.GetSteamIdIfValidMessageAsync(parts);
        //if (!steamIdOptional.IsSpecified) return;

        var steamId = ulong.Parse(parts[1]);
        var userId = message.Author.Id;

        var request = new RegisterPlayerRequest { SteamId = steamId, UserId = userId };
        var registeredplayer = await steamClient.RegisterPlayerAsync(request);

        var registerMessage = MessagesFormatter.FormatRegisterPlayer(
            registeredplayer.UserId,
            registeredplayer.SteamId
        );

        Console.WriteLine($"{nameof(RegisterPlayerAsync)}: " + message.Content);
        await message.Channel.SendMessageAsync($"You have successfully registered as {registeredplayer.SteamId} (SteamID: {steamId})!");
    }

    private async Task ShowProfileAsync(SocketMessage message)
    {
        var profileResponse = await steamClient.ShowProfileAsync(message.Author.Id);
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

    private async Task CreateLobbyAsync(SocketMessage message)
    {
        var lobbyResponse = await steamClient.CreateLobbyAsync(message.Author.Id);
        if (lobbyResponse == null) return;

        var createLobbyMessage = MessagesFormatter.FormatCreateLobby(
            lobbyResponse.LobbyName,
            lobbyResponse.LobbyId
        );

        await message.Channel.SendMessageAsync(createLobbyMessage);
    }

    private async Task ShowCommandsAsync(SocketMessage message)
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
}