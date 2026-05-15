using Discord;
using Discord.WebSocket;
using IHLDota2Bot.Endpoints.Discord;
using SteamKit2;

namespace IHLDota2Bot.Services;

internal enum DiscordAction
{
    REGISTER,
    PROFILE,
    MATCH,
    PICK,
    COMMANDS
};

internal enum DiscordResponse
{
    UNKNOWN_COMMAND,
    NOT_REGISTERED,
    INVALID_REGISTER_COMMAND,
    ALREADY_REGISTERED,
    ALREADY_IN_ACTIVE_MATCH,
    NOT_ENOUGH_PLAYERS_FOR_A_MATCH,
    SHOW_COMMANDS
};

public class DiscordService
{
    // @TODO VERY IMPORTANT, REGENERATE THIS AND PUT IT IN A FILE NOT STORED ON GITHUB
    private const string DiscordToken = "MTM3MzQwMTIxMjkzNzgzMDU3Mg.GwOJf5.ZBOX0Fm6od95W77Ow7XDiZVgJ8a9Bqh4EaFiWE";
    private static readonly Dictionary<string, DiscordAction> ActionMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["register"] = DiscordAction.REGISTER,
            ["profile"] = DiscordAction.PROFILE,
            ["match"] = DiscordAction.MATCH,
            ["pick"] = DiscordAction.PICK,
            ["comenzi"] = DiscordAction.COMMANDS
        };
    private static readonly Dictionary<DiscordResponse, string> ResponseMap =
        new()
        {
            [DiscordResponse.UNKNOWN_COMMAND] = "Commanda nu este valida. Scrie !comenzi pentru a vedea lista activa de comande.",
            [DiscordResponse.NOT_REGISTERED] = "You are not registered. Use `!register <SteamID64>`",
            [DiscordResponse.INVALID_REGISTER_COMMAND] = "Use: `!register <SteamID64>`",
            [DiscordResponse.ALREADY_REGISTERED] = "You are already registered.",
            [DiscordResponse.ALREADY_IN_ACTIVE_MATCH] = "You are already in an active match.",
            [DiscordResponse.NOT_ENOUGH_PLAYERS_FOR_A_MATCH] = "There are not enough players available for a match (minimum 10).",
            [DiscordResponse.SHOW_COMMANDS] =
                "╔══════════════════════════════════╗\n" +
                "       🎮  **IHL Dota 2 Bot**  🎮      \n" +
                "╚══════════════════════════════════╝\n" +
                "\n" +
                "📋  __**Lista de Comenzi**__\n" +
                "\n" +
                "> 📝  `!register` — Înregistrează-te în ligă\n" +
                "> ❓  `!comenzi` — Afișează acest meniu\n" +
                "\n" +
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
                "💡 *Toate comenzile încep cu* `!`\n" +
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
        };
    private bool isRunning;
    private readonly DiscordSocketClient discordClient;

    // @TODO The data needed for `players`/player functionality should be received 
    // by making a GET request(or direct call if possible) to the steam service, for now we fake it
    public class Player
    {
        public ulong UserId { get; set; }
        public string Username { get; set; }
        public ulong SteamId { get; set; }
        public int Rating { get; set; }
        public int Matches { get; set; }
        public int Wins { get; set; }
    }
    public class Match
    {
        public Guid Id { get; set; }
        public Player Captain1 { get; set; }
        public Player Captain2 { get; set; }
        public List<Player> Team1 { get; set; }
        public List<Player> Team2 { get; set; }
        public List<Player> DraftPool { get; set; }
        public List<(ulong CaptainId, string PlayerName)> Picks { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public ulong NextCaptainId { get; set; } // pentru snake draft
    }
    private static Dictionary<ulong, Player> players = new();
    private static Dictionary<ulong, Guid> activeCaptainMatches = new();
    private static List<Match> matches = [];
    // END TODO
    
    public DiscordService()
    {
        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent  
        };
        discordClient = new DiscordSocketClient(config);
        discordClient.MessageReceived += HandleMessageAsync;
    }

    public async Task<bool> StartServer(DiscordStartUpRequest request)
    {
        isRunning = true;

        await discordClient.LoginAsync(TokenType.Bot, DiscordToken);
        await discordClient.StartAsync();

        return true;
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
        var command = parts[0];

        if (!ActionMap.TryGetValue(command, out var action))
        {
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.UNKNOWN_COMMAND]);
            return;
        }

        switch (action)
        {
            case DiscordAction.REGISTER:
                await RegisterPlayerAsync(message, parts);
                break;
            case DiscordAction.PROFILE:
                await ShowProfileAsync(message);
                break;
            case DiscordAction.MATCH:
                await CreateDraftMatchAsync(message);
                break;
            case DiscordAction.PICK:
                await HandlePickAsync(message);
                break;
            case DiscordAction.COMMANDS:
                await ShowCommandsAsync(message);
                break;
        }
    }

    private static async Task<Optional<ulong>> GetSteamIdIfValidMessage(SocketMessage message, string[] message_list)
    {
        bool is_valid = true;

        if (message_list.Length < 2)
        {
            is_valid = false;
        }
        if (!ulong.TryParse(message_list[1], out ulong steamId))
        {
            is_valid = false;
        }
        // Basic SteamId check (doesn't check if it exists, only if it "looks" like one)
        if (!is_valid ||
            steamId < 10000000000000000UL ||    // fewer than 17 digits
            steamId > 99999999999999999UL       // more than 17 digits
        )
        {
            Console.WriteLine($"{nameof(RegisterPlayerAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.INVALID_REGISTER_COMMAND]);
            return Optional<ulong>.Unspecified;
        }

        return steamId;
    }

    private async Task RegisterPlayerAsync(SocketMessage message, string[] message_list)
    {
        var steamIdOptional = await GetSteamIdIfValidMessage(message, message_list);
        if (!steamIdOptional.IsSpecified) return;

        var steamId = steamIdOptional.Value;
        var userId = message.Author.Id;

        if (players.ContainsKey(userId))
        {
            Console.WriteLine($"{nameof(RegisterPlayerAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.ALREADY_REGISTERED]);
            return;
        }
        var player = new Player
        {
            UserId = userId,
            Username = message.Author.Username,
            SteamId = steamId,
            Rating = 1000,
            Matches = 0,
            Wins = 0
        };
        players[userId] = player;
        // SavePlayers();
        Console.WriteLine($"{nameof(RegisterPlayerAsync)}: " + message.Content);
        await message.Channel.SendMessageAsync($"You have successfully registered as {player.Username} (SteamID: {steamId})!");
    }

    private async Task ShowProfileAsync(SocketMessage message)
    {
        if (!players.TryGetValue(message.Author.Id, out var player))
        {
            Console.WriteLine($"{nameof(ShowProfileAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.NOT_REGISTERED]);
            return;
        }

        var profile = $"**Profile for {player.Username}**\n" +
                        $"- SteamID: {player.SteamId}\n" +
                        $"- Rating: {player.Rating}\n" +
                        $"- Matches: {player.Matches}\n" +
                        $"- Wins: {player.Wins}";

        Console.WriteLine($"{nameof(ShowProfileAsync)}: " + message.Content);
        await message.Channel.SendMessageAsync(profile);
    }

    private async Task CreateDraftMatchAsync(SocketMessage message)
    {
        if (!players.TryGetValue(message.Author.Id, out var captain1))
        {
            Console.WriteLine($"{nameof(CreateDraftMatchAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.NOT_REGISTERED]);
            return;
        }

        if (activeCaptainMatches.ContainsKey(message.Author.Id))
        {
            Console.WriteLine($"{nameof(CreateDraftMatchAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.ALREADY_IN_ACTIVE_MATCH]);
            return;
        }

        var availablePlayers = players.Values
            .Where(p => p.UserId != captain1.UserId)
            .OrderByDescending(p => p.Rating)
            .Take(8)
            .ToList();

        if (availablePlayers.Count < 9)
        {
            Console.WriteLine($"{nameof(CreateDraftMatchAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.NOT_ENOUGH_PLAYERS_FOR_A_MATCH]);
            return;
        }

        var captain2 = availablePlayers[0];
        availablePlayers.RemoveAt(0);

        var match = new Match
        {
            Id = Guid.NewGuid(),
            Captain1 = captain1,
            Captain2 = captain2,
            Team1 = new List<Player> { captain1 },
            Team2 = new List<Player> { captain2 },
            DraftPool = availablePlayers,
            Picks = new(),
            CreatedAt = DateTime.UtcNow,
            NextCaptainId = captain1.UserId
        };

        activeCaptainMatches[captain1.UserId] = match.Id;
        activeCaptainMatches[captain2.UserId] = match.Id;
        matches.Add(match);
        // SaveMatches();

        //await dotaLobbyManager.CreateLobbyAsync("AATEST", "123123");

        await message.Channel.SendMessageAsync($"Match created between {captain1.Username} and {captain2.Username}. Draft begins!");   
    }

    private async Task HandlePickAsync(SocketMessage message)
    {
        // @todo
    }

    private async Task ReportMatchResultAsync(SocketMessage message)
    {
        // @todo
    }

    private async Task ShowLeaderboardAsync(SocketMessage message)
    {
        // @todo
    }

    private async Task ShowRecentMatchesAsync(SocketMessage message)
    {
        // @todo
    }

    private async Task ShowMyStatsAsync(SocketMessage message)
    {
        // @todo
    }

    private async Task ChallengePlayerAsync(SocketMessage message)
    {
        // @todo
    }

    private async Task AcceptChallengeAsync(SocketMessage message)
    {
        // @todo
    }

    private async Task DeclineChallengeAsync(SocketMessage message)
    {
        // @todo
    }

    private async Task ReportMatch1v1Async(SocketMessage message)
    {
        // @todo
    }

    private async Task ShowCommandsAsync(SocketMessage message)
    {
        Console.WriteLine($"{nameof(ShowCommandsAsync)}: " + message.Content);
        await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.SHOW_COMMANDS]);
    }
}