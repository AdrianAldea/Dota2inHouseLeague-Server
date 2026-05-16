using Discord;
using Discord.WebSocket;
using IHLDota2Bot.Endpoints.Discord;
using SteamKit2;

namespace IHLDota2Bot.Services;

// @todo This should be moved into a file, internal to `DiscordService`
internal enum DiscordAction
{
    REGISTER,
    PROFILE,
    MATCH,
    PICK,
    REPORT,
    LEADERBOARD,
    MATCHES,
    MYSTATS,
    CHALLENGE,
    ACCEPT_MATCH,
    DECLINE_MATCH,
    REPORT_MATCH,
    CREATE_GAME,
    COMMANDS
};

// @todo This should be moved into a shared file
public enum DiscordResponse
{
    UNKNOWN_COMMAND,
    NOT_REGISTERED,
    INVALID_REGISTER_COMMAND,
    ALREADY_REGISTERED,
    ALREADY_IN_ACTIVE_MATCH,
    NOT_ENOUGH_PLAYERS_FOR_A_MATCH,
    NOT_IN_ACTIVE_DRAFT,
    INVALID_PICK_COMMAND,
    NOT_YOUR_TURN_TO_CHOOSE_IN_DRAFT,
    PLAYER_NOT_AVAILABLE_TO_PICK,
    DRAFT_COMPLETE,
    INVALID_REPORT_COMMAND,
    NO_MATCH_TO_REPORT,
    TRIED_TO_CHALLENGE_WHILE_UNREGISTERED,
    INVALID_CHALLENGE_COMMAND,
    CHALLENGED_PLAYER_NOT_REGISTERED,
    INVALID_ACCEPT_CHALLENGE_COMMAND,
    NO_ACTIVE_CHALLENGE_FROM_PLAYER,
    INVALID_DECLINE_CHALLENGE_COMMAND,
    INVALID_REPORT_MATCH_COMMAND,
    NO_RECENT_1V1_MATCHES,
    USER_NOT_VALID_FOR_1V1_MATCH,
    SHOW_COMMANDS,
    UNKNOWN_ERROR
};

public struct DiscordFailure
{
    // The failure code. If this is set, `FailureReason` should not be, and viceversa(there's probably a better way of doing this)
    public Optional<DiscordResponse> FailureCode;
    public Optional<string> FailureReason;
};

// @TODO This needs to be moved probably into a shared folder for both the database and discord
// to have access to
public struct GetOrCreatePlayerResponse
{
    public Optional<DiscordFailure> Failure;
    public struct SuccessData
    {
        public string PlayerUsername { get; set; }
    };
    public Optional<SuccessData> Data;
};

// @TODO This needs to be moved probably into a shared folder for both the database and discord
// to have access to
public struct PlayerInformation
{
    public string Username { get; set; }
    public ulong SteamId { get; set; }
    public int Rating { get; set; }
    public int Matches { get; set; }
    public int Wins { get; set; }
}

// @TODO This needs to be moved probably into a shared folder for both the database and discord
// to have access to
public struct GetPlayerResponse
{
    public Optional<DiscordFailure> Failure;
    public struct SuccessData
    {
        public PlayerInformation _PlayerInformation { get; set; }
    };
    public Optional<SuccessData> Data;
};

// @TODO This needs to be moved probably into a shared folder for both the game coordinator and discord
// to have access to
public struct MakeNewMatchResponse
{
    public Optional<DiscordFailure> Failure;
    public struct SuccessData
    {
        public Guid MatchId { get; set; }
        public string Captain1Username { get; set; }
        public string Captain2Username { get; set; }
    };
    public Optional<SuccessData> Data;
};

// @TODO This needs to be moved probably into a shared folder for both the game coordinator and discord
// to have access to
public struct CreateDraftMatchResponse
{
    public Optional<DiscordFailure> Failure;
    public struct SuccessData
    {
        public string MatchName { get; set; }
        public string PickedUsername { get; set; }
    };
    public Optional<SuccessData> Data;
};

// @TODO This needs to be moved probably into a shared folder for both the database/game coordinator(not sure yet which one needs to handle this) and discord
// to have access to
public struct GetMatchResultsResponse
{
    public Optional<DiscordFailure> Failure;
    public struct SuccessData
    {
        public int WinningTeam { get; set; }
    };
    public Optional<SuccessData> Data;
};

public struct GetLeaderboardResponse
{
    public Optional<DiscordFailure> Failure;
    public struct SuccessData
    {
        public string Leaderboard { get; set; }
    };
    public Optional<SuccessData> Data;
};

public struct GetRecentMatchesResponse
{
    public Optional<DiscordFailure> Failure;
    public struct SuccessData
    {
        public string Report { get; set; }
    };
    public Optional<SuccessData> Data;
};

public struct GetPlayerStatsResponse
{
    public Optional<DiscordFailure> Failure;
    public struct SuccessData
    {
        public PlayerInformation _PlayerInformation { get; set; }
        public double winRate;
        public int rank; // @todo This should be an enum eventually if dota ranks are like league of legends?
    };
    public Optional<SuccessData> Data;
};

public class DiscordService
{
    // @TODO VERY IMPORTANT, REGENERATE THIS AND PUT IT IN A SECRETS FILE OR PASS BY CLI NOT STORED ON GITHUB
    private const string DiscordToken = "MTM3MzQwMTIxMjkzNzgzMDU3Mg.GwOJf5.ZBOX0Fm6od95W77Ow7XDiZVgJ8a9Bqh4EaFiWE";
    private static readonly Dictionary<string, DiscordAction> ActionMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["register"] = DiscordAction.REGISTER,
            ["profile"] = DiscordAction.PROFILE,
            ["match"] = DiscordAction.MATCH,
            ["pick"] = DiscordAction.PICK,
            ["report"] = DiscordAction.REPORT,
            ["leaderboard"] = DiscordAction.LEADERBOARD,
            ["matches"] = DiscordAction.MATCHES,
            ["mystats"] = DiscordAction.MYSTATS,
            ["challenge"] = DiscordAction.CHALLENGE,
            ["acceptmatch"] = DiscordAction.ACCEPT_MATCH,
            ["declinematch"] = DiscordAction.DECLINE_MATCH,
            ["reportmatch"] = DiscordAction.REPORT_MATCH,
            ["creategame"] = DiscordAction.CREATE_GAME,
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
            [DiscordResponse.NOT_IN_ACTIVE_DRAFT] = "You are not in an active draft.",
            [DiscordResponse.NOT_YOUR_TURN_TO_CHOOSE_IN_DRAFT] = "It's not your turn to choose.",
            [DiscordResponse.INVALID_PICK_COMMAND] = "Use: `!pick <player_name>`",
            [DiscordResponse.PLAYER_NOT_AVAILABLE_TO_PICK] = "Player is not available for pick.",
            [DiscordResponse.DRAFT_COMPLETE] = "The draft is complete. The game can be played!",
            [DiscordResponse.INVALID_REPORT_COMMAND] = "Use: `!report <1 or 2>` for the winning team.",
            [DiscordResponse.NO_MATCH_TO_REPORT] = "There is no match to report.",
            [DiscordResponse.TRIED_TO_CHALLENGE_WHILE_UNREGISTERED] = "You have to be registered to challenge someone.",
            [DiscordResponse.INVALID_CHALLENGE_COMMAND] = "Use: `!challenge @jucator`",
            [DiscordResponse.CHALLENGED_PLAYER_NOT_REGISTERED] = "The challenged player is not registered.",
            [DiscordResponse.INVALID_ACCEPT_CHALLENGE_COMMAND] = "Use: `!acceptmatch @player`",
            [DiscordResponse.NO_ACTIVE_CHALLENGE_FROM_PLAYER] = "There is no active challenge from this player.",
            [DiscordResponse.INVALID_DECLINE_CHALLENGE_COMMAND] = "Use: `!declinematch @player`",
            [DiscordResponse.INVALID_REPORT_MATCH_COMMAND] = "Use: `!reportmatch <userId_câștigător>`",
            [DiscordResponse.NO_RECENT_1V1_MATCHES] = "There are no recent 1v1 matches to report.",
            [DiscordResponse.USER_NOT_VALID_FOR_1V1_MATCH] = "The specified winner is not valid for the last 1v1 match.",
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
                "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━",
            [DiscordResponse.UNKNOWN_ERROR] = "An unknown error occurred"
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
    // private static Dictionary<ulong, Player> players = new();
    private static Dictionary<ulong, Guid> activeCaptainMatches = new();
    private static List<Match> matches = [];
    private static List<(ulong ChallengerId, ulong OpponentId)> pendingChallenges = new();
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
                await HandlePickAsync(message, parts);
                break;
            case DiscordAction.REPORT:
                await HandleMatchResultAsync(message, parts);
                break;
            case DiscordAction.LEADERBOARD:
                await ShowLeaderboardAsync(message);
                break;
            case DiscordAction.MATCHES:
                await ShowRecentMatchesAsync(message);
                break;
            case DiscordAction.MYSTATS:
                await ShowMyStatsAsync(message);
                break;
            case DiscordAction.CHALLENGE:
                await ChallengePlayerAsync(message);
                break;
            case DiscordAction.ACCEPT_MATCH:
                await AcceptChallengeAsync(message);
                break;
            case DiscordAction.DECLINE_MATCH:
                await DeclineChallengeAsync(message);
                break;
            case DiscordAction.REPORT_MATCH:
                await ReportMatch1v1Async(message, parts);
                break;
            case DiscordAction.COMMANDS:
                await ShowCommandsAsync(message);
                break;
        }
    }

    // Utility function to make a message from a `DiscordFailure`
    private static string MakeMessageFromFailure(DiscordFailure failure)
    {
        return
            failure.FailureCode.IsSpecified ?
            ResponseMap[failure.FailureCode.Value] :
            failure.FailureReason.IsSpecified ?
            failure.FailureReason.Value :
            ResponseMap[DiscordResponse.UNKNOWN_ERROR];
    }

    // Utility function to report an error if there was any
    // @return true if there was an error and should return
    private static async Task<bool> TryReportError(
        SocketMessage message,
        string functionName,
        Optional<DiscordFailure> failureOptional
    )
    {
        if (!failureOptional.IsSpecified) 
            return false;

        var failureReason = MakeMessageFromFailure(failureOptional.Value);
        Console.WriteLine($"{functionName}: " + message.Content);
        await message.Channel.SendMessageAsync(failureReason);
        return true;
    }

    // Utility function to extract some `SuccessData` from a generic response
    // @return tuple where first item is success/failure to retrieve the data
    // and second is the actual value
    private static async Task<(bool Success, T value)> TryGetData<T>(
        SocketMessage message,
        string functionName,
        Optional<T> optional
    )
    {
        if (!optional.IsSpecified)
        {
            Console.WriteLine($"{functionName}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.UNKNOWN_ERROR]);
            return (false, default!);
        }

        return (true, optional.Value);
    }

    // Quick check to save on most invalid database calls
    private static async Task<Optional<ulong>> GetSteamIdIfValidMessageAsync(SocketMessage message, string[] message_list)
    {
        bool is_valid = message_list.Length >= 2;

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
            Console.WriteLine($"{nameof(GetSteamIdIfValidMessageAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.INVALID_REGISTER_COMMAND]);
            return Optional<ulong>.Unspecified;
        }

        return steamId;
    }

    private async Task RegisterPlayerAsync(SocketMessage message, string[] messageList)
    {
        var steamIdOptional = await GetSteamIdIfValidMessageAsync(message, messageList);
        if (!steamIdOptional.IsSpecified) return;

        var steamId = steamIdOptional.Value;
        var userId = message.Author.Id;

        GetOrCreatePlayerResponse response = await DispatchGetOrCreatePlayerAsync(userId, steamId);
        if (!await TryReportError(message, nameof(RegisterPlayerAsync), response.Failure)) return;
        var (success, data) = await TryGetData(message, nameof(RegisterPlayerAsync), response.Data);
        if (!success) return;

        Console.WriteLine($"{nameof(RegisterPlayerAsync)}: " + message.Content);
        await message.Channel.SendMessageAsync($"You have successfully registered as {data.PlayerUsername} (SteamID: {steamId})!");
    }

    private async Task ShowProfileAsync(SocketMessage message)
    {
        ulong authorId = message.Author.Id;

        GetPlayerResponse response = await DispatchGetPlayerAsync(authorId);
        if (!await TryReportError(message, nameof(ShowProfileAsync), response.Failure)) return;
        var (success, data) = await TryGetData(message, nameof(ShowProfileAsync), response.Data);
        if (!success) return;

        var profile = $"**Profile for {data._PlayerInformation.Username}**\n" +
                        $"- SteamID: {data._PlayerInformation.SteamId}\n" +
                        $"- Rating: {data._PlayerInformation.Rating}\n" +
                        $"- Matches: {data._PlayerInformation.Matches}\n" +
                        $"- Wins: {data._PlayerInformation.Wins}";

        Console.WriteLine($"{nameof(ShowProfileAsync)}: " + message.Content);
        await message.Channel.SendMessageAsync(profile);
    }

    private async Task CreateDraftMatchAsync(SocketMessage message)
    {
        ulong authorId = message.Author.Id;

        MakeNewMatchResponse response = await DispatchMakeNewMatchAsync(authorId);
        if (!await TryReportError(message, nameof(CreateDraftMatchAsync), response.Failure)) return;
        var (success, data) = await TryGetData(message, nameof(CreateDraftMatchAsync), response.Data);
        if (!success) return;

        // @TODO This logic should be done by the game coordinator when a `DispatchMakeNewMatchAsync` call is made
        // if (activeCaptainMatches.ContainsKey(message.Author.Id))
        // {
        //     Console.WriteLine($"{nameof(CreateDraftMatchAsync)}: " + message.Content);
        //     await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.ALREADY_IN_ACTIVE_MATCH]);
        //     return;
        // }

        // var availablePlayers = players.Values
        //     .Where(p => p.UserId != captain1.UserId)
        //     .OrderByDescending(p => p.Rating)
        //     .Take(8)
        //     .ToList();

        // if (availablePlayers.Count < 9)
        // {
        //     Console.WriteLine($"{nameof(CreateDraftMatchAsync)}: " + message.Content);
        //     await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.NOT_ENOUGH_PLAYERS_FOR_A_MATCH]);
        //     return;
        // }

        // var captain2 = availablePlayers[0];
        // availablePlayers.RemoveAt(0);

        // var match = new Match
        // {
        //     Id = Guid.NewGuid(),
        //     Captain1 = captain1,
        //     Captain2 = captain2,
        //     Team1 = new List<Player> { captain1 },
        //     Team2 = new List<Player> { captain2 },
        //     DraftPool = availablePlayers,
        //     Picks = new(),
        //     CreatedAt = DateTime.UtcNow,
        //     NextCaptainId = captain1.UserId
        // };

        // activeCaptainMatches[captain1.UserId] = match.Id;
        // activeCaptainMatches[captain2.UserId] = match.Id;
        // matches.Add(match);
        // @TODO This one is no longer needed as the game coordinator should make the call to the database
        // to save the match
        // await DispatchSaveMatchAsync(matchId);

        Console.WriteLine($"{nameof(CreateDraftMatchAsync)}: " + message.Content);
        await DispatchCreateLobbyAsync("AATEST", 123123UL); // @todo perhaps the game coordinator should also handle this within the `DispatchMakeNewMatchAsync` ?
        await message.Channel.SendMessageAsync($"Match created between {data.Captain1Username} and {data.Captain2Username}. Draft begins!");
    }

    private async Task HandlePickAsync(SocketMessage message, string[] messageList)
    {
        var userId = message.Author.Id;

        CreateDraftMatchResponse response = await DispatchCreateDraftMatchAsync(userId);
        if (!await TryReportError(message, nameof(HandlePickAsync), response.Failure)) return;
        var (success, data) = await TryGetData(message, nameof(HandlePickAsync), response.Data);
        if (!success) return;
        
        // @TODO This logic should be handled by the game coordinator
        // if (!activeCaptainMatches.TryGetValue(userId, out var matchId))
        // {
        //     Console.WriteLine($"{nameof(HandlePickAsync)}: " + message.Content);
        //     await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.NOT_IN_ACTIVE_DRAFT]);
        //     return;
        // }

        // var match = matches.FirstOrDefault(m => m.Id == matchId);
        // if (match == null || match.NextCaptainId != userId)
        // {
        //     Console.WriteLine($"{nameof(HandlePickAsync)}: " + message.Content);
        //     await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.NOT_YOUR_TURN_TO_CHOOSE_IN_DRAFT]);
        //     return;
        // }

        // if (message_list.Length < 2)
        // {
        //     Console.WriteLine($"{nameof(HandlePickAsync)}: " + message.Content);
        //     await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.INVALID_PICK_COMMAND]);
        //     return;
        // }

        // var pickName = message_list[1].Trim();
        // var pick = match.DraftPool.FirstOrDefault(p => p.Username.Equals(pickName, StringComparison.OrdinalIgnoreCase));
        // if (pick == null)
        // {
        //     Console.WriteLine($"{nameof(HandlePickAsync)}: " + message.Content);
        //     await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.PLAYER_NOT_AVAILABLE_TO_PICK]);
        //     return;
        // }

        // if (userId == match.Captain1.UserId)
        //     match.Team1.Add(pick);
        // else
        //     match.Team2.Add(pick);

        // match.DraftPool.Remove(pick);
        // match.Picks.Add((userId, pick.Username));
        // match.NextCaptainId = match.NextCaptainId == match.Captain1.UserId ? match.Captain2.UserId : match.Captain1.UserId;
        // await DispatchSaveMatchesAsync();

        // await message.Channel.SendMessageAsync($"{message.Author.Username} chose {pick.Username}.");

        // if (match.DraftPool.Count == 0)
        // {
        //     await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.DRAFT_COMPLETE]);
        //     activeCaptainMatches.Remove(match.Captain1.UserId);
        //     activeCaptainMatches.Remove(match.Captain2.UserId);
        // }

        Console.WriteLine($"{nameof(HandlePickAsync)}: " + message.Content + $"\nMatch: {data.MatchName}");
        await message.Channel.SendMessageAsync($"{message.Author.Username} chose {data.PickedUsername}.");
    }

    // @todo Perhaps better algorithm
    // @todo Needs to be moved into game coordinator
    private static void HandleWinningPlayer(Player player)
    {
        player.Rating += 25;
        ++player.Wins;
        ++player.Matches;
    }

    // @todo Perhaps better algorithm
    // @todo Needs to be moved into game coordinator
    private static void HandleLosingPlayer(Player player)
    {
        player.Rating -= 15;
        ++player.Matches;
    }

    private async Task HandleMatchResultAsync(SocketMessage message, string[] messageList)
    {
        if (messageList.Length < 2 ||
            !int.TryParse(messageList[1], out int teamChosen) ||
            (teamChosen != 1 && teamChosen != 2))
        {
            Console.WriteLine($"{nameof(HandleMatchResultAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.INVALID_REPORT_COMMAND]);
            return;
        }

        GetMatchResultsResponse response = await DispatchGetMatchResultsAsync(teamChosen);
        if (!await TryReportError(message, nameof(HandleMatchResultAsync), response.Failure)) return;
        var (success, data) = await TryGetData(message, nameof(HandleMatchResultAsync), response.Data);
        if (!success) return;

        // The database/game coordinator should be responsible for this logic
        // var lastMatch = matches.LastOrDefault();
        // if (lastMatch == null)
        // {
        //     Console.WriteLine($"{nameof(HandleMatchResultAsync)}: " + message.Content);
        //     await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.NO_MATCH_TO_REPORT]);
        //     return;
        // }

        // var winners = winningTeam == 1 ? lastMatch.Team1 : lastMatch.Team2;
        // var losers = winningTeam == 1 ? lastMatch.Team2 : lastMatch.Team1;

        // foreach (var p in winners)
        // {
        //     HandleWinningPlayer(p);
        // }

        // foreach (var p in losers)
        // {
        //     HandleLosingPlayer(p);
        // }

        // SavePlayers();
        // SaveMatches();

        // @todo Maybe report the name instead of the number of the team if dota has such a thing?
        Console.WriteLine($"{nameof(HandleMatchResultAsync)}: " + message.Content);
        await message.Channel.SendMessageAsync($"The match has been reported. Team {data.WinningTeam} won.");
    }

    private async Task ShowLeaderboardAsync(SocketMessage message)
    {
        GetLeaderboardResponse response = await DispatchGetLeaderboardAsync();
        if (!await TryReportError(message, nameof(ShowLeaderboardAsync), response.Failure)) return;
        var (success, data) = await TryGetData(message, nameof(ShowLeaderboardAsync), response.Data);
        if (!success) return;

        // This logic should be handled by database
        // var topPlayers = players.Values.OrderByDescending(p => p.Rating).Take(10);
        // var leaderboard = "**Ranking Top 10**\n";

        // int rank = 1;
        // foreach (var p in topPlayers)
        // {
        //     leaderboard += $"{rank++}. {p.Username} - {p.Rating} rating\n";
        // }

        Console.WriteLine($"{nameof(ShowLeaderboardAsync)}: " + message.Content + $", leaderboard: {data.Leaderboard}");
        await message.Channel.SendMessageAsync(data.Leaderboard);
    }

    private async Task ShowRecentMatchesAsync(SocketMessage message)
    {
        GetRecentMatchesResponse response = await DispatchGetRecentMatchesAsync();
        if (!await TryReportError(message, nameof(ShowRecentMatchesAsync), response.Failure)) return;
        var (success, data) = await TryGetData(message, nameof(ShowRecentMatchesAsync), response.Data);
        if (!success) return;

        // This logic should be handled by database
        // var recent = matches.TakeLast(5);
        // string report = "**Last games:**\n";

        // foreach (var m in recent)
        // {
        //     report += $"- {m.Id} | {m.Captain1.Username} vs {m.Captain2.Username} | {m.CreatedAt:dd/MM/yyyy HH:mm}\n";
        // }

        Console.WriteLine($"{nameof(ShowRecentMatchesAsync)}: " + message.Content + $", report: {data.Report}");
        await message.Channel.SendMessageAsync(data.Report);
    }

    private async Task ShowMyStatsAsync(SocketMessage message)
    {
        var authorId = message.Author.Id;

        GetPlayerStatsResponse response = await DispatchGetPlayerStatsAsync(authorId);
        if (!await TryReportError(message, nameof(ShowMyStatsAsync), response.Failure)) return;
        var (success, data) = await TryGetData(message, nameof(ShowMyStatsAsync), response.Data);
        if (!success) return;

        // This logic should be the responsability of the database
        // double winRate = player.Matches > 0 ? (double)player.Wins / player.Matches * 100 : 0;
        // var sorted = players.Values.OrderByDescending(p => p.Rating).ToList();
        // var rank = sorted.FindIndex(p => p.UserId == player.UserId) + 1;

        string stats = $"**Stats for {data._PlayerInformation.Username}:**\n" +
                        $"- Rating: {data._PlayerInformation.Rating}\n" +
                        $"- Matches: {data._PlayerInformation.Matches}\n" +
                        $"- Wins: {data._PlayerInformation.Wins}\n" +
                        $"- Winrate: {data.winRate:F2}%\n" +
                        $"- Rank: #{data.rank}";

        Console.WriteLine($"{nameof(ShowMyStatsAsync)}: " + message.Content + $", stats: {stats}");
        await message.Channel.SendMessageAsync(stats);
    }

    private async Task ChallengePlayerAsync(SocketMessage message)
    {
        if (!players.TryGetValue(message.Author.Id, out _))
        {
            Console.WriteLine($"{nameof(ChallengePlayerAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.TRIED_TO_CHALLENGE_WHILE_UNREGISTERED]);
            return;
        }

        if (message.MentionedUsers.Count == 0)
        {
            Console.WriteLine($"{nameof(ChallengePlayerAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.INVALID_CHALLENGE_COMMAND]);
            return;
        }

        var opponent = message.MentionedUsers.First();
        if (!players.ContainsKey(opponent.Id))
        {
            Console.WriteLine($"{nameof(ChallengePlayerAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.CHALLENGED_PLAYER_NOT_REGISTERED]);
            return;
        }

        pendingChallenges.Add((message.Author.Id, opponent.Id));
        await message.Channel.SendMessageAsync($"{opponent.Username}, you have been challenged by {message.Author.Username}! Respond with `!acceptmatch @{message.Author.Username}` or `!declinematch @{message.Author.Username}`.");
    }

    private async Task AcceptChallengeAsync(SocketMessage message)
    {
        if (message.MentionedUsers.Count == 0)
        {
            Console.WriteLine($"{nameof(AcceptChallengeAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.INVALID_ACCEPT_CHALLENGE_COMMAND]);
            return;
        }

        var challenger = message.MentionedUsers.First();
        var opponent = message.Author;

        if (!pendingChallenges.Contains((challenger.Id, opponent.Id)))
        {
            Console.WriteLine($"{nameof(AcceptChallengeAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.NO_ACTIVE_CHALLENGE_FROM_PLAYER]);
            return;
        }

        pendingChallenges.Remove((challenger.Id, opponent.Id));

        var match = new Match
        {
            Id = Guid.NewGuid(),
            Captain1 = players[challenger.Id],
            Captain2 = players[opponent.Id],
            Team1 = new List<Player> { players[challenger.Id] },
            Team2 = new List<Player> { players[opponent.Id] },
            DraftPool = new(),
            Picks = new(),
            CreatedAt = DateTime.UtcNow
        };

        matches.Add(match);
        // SaveMatches();
        Console.WriteLine($"{nameof(AcceptChallengeAsync)}: " + message.Content + $", challenge between: {challenger.Username} and {opponent.Username} with match ID: {match.Id}");
        await message.Channel.SendMessageAsync($"1v1 match created between {challenger.Username} and {opponent.Username}! Match ID: {match.Id}");
    }

    private async Task DeclineChallengeAsync(SocketMessage message)
    {
        if (message.MentionedUsers.Count == 0)
        {
            Console.WriteLine($"{nameof(DeclineChallengeAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.INVALID_DECLINE_CHALLENGE_COMMAND]);
            return;
        }

        var challenger = message.MentionedUsers.First();
        var opponent = message.Author;

        if (!pendingChallenges.Remove((challenger.Id, opponent.Id)))
        {
            Console.WriteLine($"{nameof(DeclineChallengeAsync)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.NO_ACTIVE_CHALLENGE_FROM_PLAYER]);
            return;
        }
    }

    private async Task<Optional<ulong>> GetWinnerIdIfValidMessage(SocketMessage message, string[] message_list)
    {
        if (message_list.Length < 2 || !ulong.TryParse(message_list[1], out ulong winnerId))
        {
            Console.WriteLine($"{nameof(GetWinnerIdIfValidMessage)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.INVALID_REPORT_MATCH_COMMAND]);
            return Optional<ulong>.Unspecified;
        }

        return winnerId;
    }

    private async Task ReportMatch1v1Async(SocketMessage message, string[] message_list)
    {
        var winnerIdOptional = await GetWinnerIdIfValidMessage(message, message_list);
        if (!winnerIdOptional.IsSpecified) return;

        var winnerId = winnerIdOptional.Value;

        var match = matches.LastOrDefault(m => m.Team1.Count == 1 && m.Team2.Count == 1);
        if (match == null)
        {
            Console.WriteLine($"{nameof(ReportMatch1v1Async)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.NO_RECENT_1V1_MATCHES]);
            return;
        }

        var winner = match.Team1.Concat(match.Team2).FirstOrDefault(p => p.UserId == winnerId);
        if (winner == null)
        {
            Console.WriteLine($"{nameof(ReportMatch1v1Async)}: " + message.Content);
            await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.USER_NOT_VALID_FOR_1V1_MATCH]);
            return;
        }

        var loser = match.Team1.Concat(match.Team2).First(p => p.UserId != winnerId);

        players[winner.UserId].Rating += 30;
        players[winner.UserId].Wins++;
        players[winner.UserId].Matches++;

        players[loser.UserId].Rating -= 20;
        players[loser.UserId].Matches++;

        // SavePlayers();
        // SaveMatches();

        Console.WriteLine($"{nameof(ReportMatch1v1Async)}: " + message.Content + $", winner: {winner.Username}, loser: {loser.Username}");
        await message.Channel.SendMessageAsync($"1v1 match reported. {winner.Username} won against {loser.Username}.");
    }

    private async Task ShowCommandsAsync(SocketMessage message)
    {
        Console.WriteLine($"{nameof(ShowCommandsAsync)}: " + message.Content);
        await message.Channel.SendMessageAsync(ResponseMap[DiscordResponse.SHOW_COMMANDS]);
    }

    private async Task<MakeNewMatchResponse> DispatchMakeNewMatchAsync(ulong userId)
    {
        // @todo Make a call to the game coordinator
        return new MakeNewMatchResponse();
    }

    private async Task<GetOrCreatePlayerResponse> DispatchGetOrCreatePlayerAsync(
        ulong userId,
        ulong steamId
    )
    {
        // @todo Make a call to the database and have it return a default `Player` structure (optional if the player already exists)
        // @todo The return should probably be something along the lines of `Task<InternalDatabaseResponse>` so that the
        // database can report additional information
        return new GetOrCreatePlayerResponse();
    }

    private async Task<GetPlayerResponse> DispatchGetPlayerAsync(ulong userId)
    {
        // @todo Make call to the database
        return new GetPlayerResponse();
    }

    // We shouldn't have this responsability, either the database internally or the game coordinator should do this
    // private async Task<Optional<Player>> DispatchMakeNewPlayerAsync(
    //     ulong userId,
    //     ulong steamId,
    //     string username
    // )
    // {
    //     // @todo Make a call to the database and have it return a default `Player` structure(optional if )
    //     return new Player();
    // }

    private async Task<CreateDraftMatchResponse> DispatchCreateDraftMatchAsync(ulong userId)
    {
        // @todo Make call to game coordinator
        return new CreateDraftMatchResponse();
    }

    private async Task<GetMatchResultsResponse> DispatchGetMatchResultsAsync(int teamChosen)
    {
        // @todo Make call to database
        return new GetMatchResultsResponse();
    }

    private async Task<GetLeaderboardResponse> DispatchGetLeaderboardAsync()
    {
        return new GetLeaderboardResponse();
    }

    private async Task<GetRecentMatchesResponse> DispatchGetRecentMatchesAsync()
    {
        return new GetRecentMatchesResponse();
    }

    private async Task<GetPlayerStatsResponse> DispatchGetPlayerStatsAsync(ulong userId)
    {
        return new GetPlayerStatsResponse();
    }

    // @TODO This would be the responsability of the database, probably after it created a new player
    // I'll leave it uncommeted to get the build working
    private async Task DispatchSavePlayersAsync()
    {
    }

    private async Task DispatchSaveMatchesAsync()
    {
        // @TODO Aici ar veni call la baza de date
    }

    private async Task DispatchCreateLobbyAsync(string lobbyName, ulong lobbyId)
    {
        // @TODO Aici ar veni call la game manager
    }
}