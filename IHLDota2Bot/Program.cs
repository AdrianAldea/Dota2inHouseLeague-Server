using System.Reflection;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using SteamKit2.GC.Dota.Internal;
using static SteamKit2.Internal.CMsgRemoteClientBroadcastStatus;

namespace IHLDota2Bot
{
    public class Program
    {
        private const string DiscordToken = "MTM3MzQwMTIxMjkzNzgzMDU3Mg.GwOJf5.ZBOX0Fm6od95W77Ow7XDiZVgJ8a9Bqh4EaFiWE";
        private const string SteamUsername = "dota2inhouseleague";
        private const string SteamPassword = "Romania90.";
        private DiscordSocketClient _client;
        private DotaLobbyManager dotaLobbyManager;
        SteamBot steamBot;
        private static Dictionary<ulong, Player> players = [];
        private static List<Match> matches = [];
        private static Dictionary<ulong, Guid> activeCaptainMatches = new();
        private static List<(ulong ChallengerId, ulong OpponentId)> pendingChallenges = new();

        public static Task Main(string[] args) => new Program().MainAsync();

        public async Task MainAsync()
        {
            steamBot = new SteamBot(SteamUsername, SteamPassword);
            _ = Task.Run( async() => await steamBot.Connect());
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
            };

            _client = new DiscordSocketClient(config);
            _client.Log += Log;
            _client.MessageReceived += HandleMessageAsync;

            LoadPlayers();
            LoadMatches();
            await _client.LoginAsync(TokenType.Bot, DiscordToken);
            await _client.StartAsync();
            await Task.Delay(-1);
        }

        static void PrintMatchDetails(CMsgDOTAMatch match)
        {
            if (match == null)
            {
                Console.WriteLine("No match details to display");
                return;
            }

            // use some lazy reflection to print out details
            var fields = typeof(CMsgDOTAMatch).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields.OrderBy(f => f.Name))
            {
                var value = field.GetValue(match, null);

                Console.WriteLine("{0}: {1}", field.Name, value);
            }
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg);
            return Task.CompletedTask;
        }

        private async Task HandleMessageAsync(SocketMessage message)
        {
            if (message.Author.IsBot) return;

            var userMessage = message as SocketUserMessage;
            if (userMessage == null) return;

            var content = userMessage.Content?.Trim();
            if (string.IsNullOrEmpty(content)) return;

            if (content.StartsWith("!register"))
                await RegisterPlayerAsync(message);
            else if (content.StartsWith("!profile"))
                await ShowProfileAsync(message);
            else if (content.StartsWith("!match"))
                await CreateDraftMatchAsync(message);
            else if (content.StartsWith("!pick"))
                await HandlePickAsync(message);
            else if (content.StartsWith("!report"))
                await ReportMatchResultAsync(message);
            else if (content.StartsWith("!leaderboard"))
                await ShowLeaderboardAsync(message);
            else if (content.StartsWith("!matches"))
                await ShowRecentMatchesAsync(message);
            else if (content.StartsWith("!mystats"))
                await ShowMyStatsAsync(message);
            else if (content.StartsWith("!challenge"))
                await ChallengePlayerAsync(message);
            else if (content.StartsWith("!acceptmatch"))
                await AcceptChallengeAsync(message);
            else if (content.StartsWith("!declinematch"))
                await DeclineChallengeAsync(message);
            else if (content.StartsWith("!reportmatch"))
                await ReportMatch1v1Async(message);
            else if (content.StartsWith("!x"))
                await CreateGame(message);
        }

        private async Task RegisterPlayerAsync(SocketMessage message)
        {
            var parts = message.Content.Split(' ');
            if (parts.Length < 2)
            {
                await message.Channel.SendMessageAsync("Use: `!register <SteamID64>`");
                return;
            }

            var steamId = parts[1];
            var userId = message.Author.Id;

            if (players.ContainsKey(userId))
            {
                await message.Channel.SendMessageAsync("You are already registered.");
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
            SavePlayers();
            await message.Channel.SendMessageAsync($"You have successfully registered as {player.Username} (SteamID: {steamId})!");
        }

        private async Task ShowProfileAsync(SocketMessage message)
        {
            if (!players.TryGetValue(message.Author.Id, out var player))
            {
                await message.Channel.SendMessageAsync("You are not registered. Use `!register <SteamID64>`");
                return;
            }

            var profile = $"**Profile for {player.Username}**\n" +
                          $"- SteamID: {player.SteamId}\n" +
                          $"- Rating: {player.Rating}\n" +
                          $"- Matches: {player.Matches}\n" +
                          $"- Wins: {player.Wins}";

            await message.Channel.SendMessageAsync(profile);
        }

        private async Task CreateDraftMatchAsync(SocketMessage message)
        {
            if (!players.TryGetValue(message.Author.Id, out var captain1))
            {
                await message.Channel.SendMessageAsync("You must be registered to create a match.");
                return;
            }

            if (activeCaptainMatches.ContainsKey(message.Author.Id))
            {
                await message.Channel.SendMessageAsync("You are already in an active match.");
                return;
            }

            var availablePlayers = players.Values
                .Where(p => p.UserId != captain1.UserId)
                .OrderByDescending(p => p.Rating)
                .Take(8)
                .ToList();

            if (availablePlayers.Count < 9)
            {
                await message.Channel.SendMessageAsync("There are not enough players available for a match (minimum 10).");
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
            SaveMatches();



            //await dotaLobbyManager.CreateLobbyAsync("AATEST", "123123");

            await message.Channel.SendMessageAsync($"Match created between {captain1.Username} and {captain2.Username}. Draft begins!");
        }

        private async Task HandlePickAsync(SocketMessage message)
        {
            var userId = message.Author.Id;
            if (!activeCaptainMatches.TryGetValue(userId, out var matchId))
            {
                await message.Channel.SendMessageAsync("You are not in an active draft.");
                return;
            }

            var match = matches.FirstOrDefault(m => m.Id == matchId);
            if (match == null || match.NextCaptainId != userId)
            {
                await message.Channel.SendMessageAsync("It's not your turn to choose.");
                return;
            }

            var parts = message.Content.Split(' ', 2);
            if (parts.Length < 2)
            {
                await message.Channel.SendMessageAsync("Use: `!pick <player_name>`");
                return;
            }

            var pickName = parts[1].Trim();
            var pick = match.DraftPool.FirstOrDefault(p => p.Username.Equals(pickName, StringComparison.OrdinalIgnoreCase));
            if (pick == null)
            {
                await message.Channel.SendMessageAsync("Player is not available for pick.");
                return;
            }

            if (userId == match.Captain1.UserId)
                match.Team1.Add(pick);
            else
                match.Team2.Add(pick);

            match.DraftPool.Remove(pick);
            match.Picks.Add((userId, pick.Username));
            match.NextCaptainId = match.NextCaptainId == match.Captain1.UserId ? match.Captain2.UserId : match.Captain1.UserId;
            SaveMatches();

            await message.Channel.SendMessageAsync($"{message.Author.Username} chose {pick.Username}.");

            if (match.DraftPool.Count == 0)
            {
                await message.Channel.SendMessageAsync("The draft is complete. The game can be played!");
                activeCaptainMatches.Remove(match.Captain1.UserId);
                activeCaptainMatches.Remove(match.Captain2.UserId);
            }
        }

        private async Task ReportMatchResultAsync(SocketMessage message)
        {
            var parts = message.Content.Split(' ');
            if (parts.Length < 2 || !int.TryParse(parts[1], out int winner))
            {
                await message.Channel.SendMessageAsync("Use: `!report <1 or 2>` for the winning team.");
                return;
            }

            var lastMatch = matches.LastOrDefault();
            if (lastMatch == null)
            {
                await message.Channel.SendMessageAsync("There is no match to report.");
                return;
            }

            var winners = winner == 1 ? lastMatch.Team1 : lastMatch.Team2;
            var losers = winner == 1 ? lastMatch.Team2 : lastMatch.Team1;

            foreach (var p in winners)
            {
                p.Rating += 25;
                p.Wins++;
                p.Matches++;
            }

            foreach (var p in losers)
            {
                p.Rating -= 15;
                p.Matches++;
            }

            SavePlayers();
            SaveMatches();

            await message.Channel.SendMessageAsync($"The match has been reported. Team {winner} won.");
        }

        private async Task ShowLeaderboardAsync(SocketMessage message)
        {
            var topPlayers = players.Values.OrderByDescending(p => p.Rating).Take(10);
            var leaderboard = "**Ranking Top 10**\n";

            int rank = 1;
            foreach (var p in topPlayers)
            {
                leaderboard += $"{rank++}. {p.Username} - {p.Rating} rating\n";
            }

            await message.Channel.SendMessageAsync(leaderboard);
        }

        private async Task ShowRecentMatchesAsync(SocketMessage message)
        {
            var recent = matches.TakeLast(5);
            string report = "**Last games:**\n";

            foreach (var m in recent)
            {
                report += $"- {m.Id} | {m.Captain1.Username} vs {m.Captain2.Username} | {m.CreatedAt:dd/MM/yyyy HH:mm}\n";
            }

            await message.Channel.SendMessageAsync(report);
        }

        private async Task ShowMyStatsAsync(SocketMessage message)
        {
            if (!players.TryGetValue(message.Author.Id, out var player))
            {
                await message.Channel.SendMessageAsync("You are not registered. Use `!register <SteamID64>`");
                return;
            }

            double winRate = player.Matches > 0 ? (double)player.Wins / player.Matches * 100 : 0;
            var sorted = players.Values.OrderByDescending(p => p.Rating).ToList();
            var rank = sorted.FindIndex(p => p.UserId == player.UserId) + 1;

            string stats = $"**Stats for {player.Username}:**\n" +
                           $"- Rating: {player.Rating}\n" +
                           $"- Matches: {player.Matches}\n" +
                           $"- Wins: {player.Wins}\n" +
                           $"- Winrate: {winRate:F2}%\n" +
                           $"- Rank: #{rank}";

            await message.Channel.SendMessageAsync(stats);
        }

        private async Task ChallengePlayerAsync(SocketMessage message)
        {
            if (!players.TryGetValue(message.Author.Id, out _))
            {
                await message.Channel.SendMessageAsync("You have to be registered to challenge someone.");
                return;
            }

            if (message.MentionedUsers.Count == 0)
            {
                await message.Channel.SendMessageAsync("Use: `!challenge @jucator`");
                return;
            }

            var opponent = message.MentionedUsers.First();
            if (!players.ContainsKey(opponent.Id))
            {
                await message.Channel.SendMessageAsync("The challenged player is not registered.");
                return;
            }

            pendingChallenges.Add((message.Author.Id, opponent.Id));
            await message.Channel.SendMessageAsync($"{opponent.Username}, you have been challenged by {message.Author.Username}! Respond with `!acceptmatch @{message.Author.Username}` or `!declinematch @{message.Author.Username}`.");
        }

        private async Task AcceptChallengeAsync(SocketMessage message)
        {
            if (message.MentionedUsers.Count == 0)
            {
                await message.Channel.SendMessageAsync("Use: `!acceptmatch @player`");
                return;
            }

            var challenger = message.MentionedUsers.First();
            var opponent = message.Author;

            if (!pendingChallenges.Contains((challenger.Id, opponent.Id)))
            {
                await message.Channel.SendMessageAsync("There is no active challenge from this player.");
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
            SaveMatches();
            await message.Channel.SendMessageAsync($"1v1 match created between {challenger.Username} and {opponent.Username}! Match ID: {match.Id}");
            ///
        }

        private async Task DeclineChallengeAsync(SocketMessage message)
        {
            if (message.MentionedUsers.Count == 0)
            {
                await message.Channel.SendMessageAsync("Use: `!declinematch @player`");
                return;
            }

            var challenger = message.MentionedUsers.First();
            var opponent = message.Author;

            if (!pendingChallenges.Remove((challenger.Id, opponent.Id)))
            {
                await message.Channel.SendMessageAsync("Nu există nicio provocare activă de la acest jucător.");
                return;
            }

            await message.Channel.SendMessageAsync($"{opponent.Username} has declined the challenge from {challenger.Username}.");
        }

        private async Task ReportMatch1v1Async(SocketMessage message)
        {
            var parts = message.Content.Split(' ');
            if (parts.Length != 2 || !ulong.TryParse(parts[1], out var winnerId))
            {
                await message.Channel.SendMessageAsync("Use: `!reportmatch <userId_câștigător>`");
                return;
            }

            var match = matches.LastOrDefault(m => m.Team1.Count == 1 && m.Team2.Count == 1);
            if (match == null)
            {
                await message.Channel.SendMessageAsync("There are no recent 1v1 matches to report.");
                return;
            }

            var winner = match.Team1.Concat(match.Team2).FirstOrDefault(p => p.UserId == winnerId);
            if (winner == null)
            {
                await message.Channel.SendMessageAsync("The specified winner is not valid for the last 1v1 match.");
                return;
            }

            var loser = match.Team1.Concat(match.Team2).First(p => p.UserId != winnerId);

            players[winner.UserId].Rating += 30;
            players[winner.UserId].Wins++;
            players[winner.UserId].Matches++;

            players[loser.UserId].Rating -= 20;
            players[loser.UserId].Matches++;

            SavePlayers();
            SaveMatches();

            await message.Channel.SendMessageAsync($"1v1 match reported. {winner.Username} won against {loser.Username}.");
        }

        private async Task CreateGame(SocketMessage message)
        {
            var lastMatch = matches.LastOrDefault();
            if (lastMatch == null)
            {
                await message.Channel.SendMessageAsync("Nu există niciun meci recent pentru a crea lobby.");
                return;
            }

            var steamIds = lastMatch.Team1.Concat(lastMatch.Team2).Select(p => p.SteamId).ToList();
            steamBot.CreateDotaLobby("D2IHLRO", "123", steamIds);

            await message.Channel.SendMessageAsync("Se creează lobby-ul și se trimit invitațiile...");
        }

        private void SavePlayers()
        {
            File.WriteAllText("players.json", JsonSerializer.Serialize(players));
        }

        private void LoadPlayers()
        {
            if (File.Exists("players.json"))
                players = JsonSerializer.Deserialize<Dictionary<ulong, Player>>(File.ReadAllText("players.json")) ?? new();
        }

        private void LoadMatches()
        {
            if (File.Exists("matches.json"))
                matches = JsonSerializer.Deserialize<List<Match>>(File.ReadAllText("matches.json")) ?? new();
        }

        private void SaveMatches()
        {
            File.WriteAllText("matches.json", JsonSerializer.Serialize(matches));
        }

    }
    public class Player
    {
        public ulong UserId { get; set; }
        public string Username { get; set; }
        public string SteamId { get; set; }
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
}
