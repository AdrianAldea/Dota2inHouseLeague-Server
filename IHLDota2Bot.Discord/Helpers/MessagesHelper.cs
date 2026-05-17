using Discord;
using Discord.WebSocket;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace IHLDota2Bot.Discord.Helpers;

public static class MessagesFormatter
{
    public static string FormatProfile(string username, string steamId, int rating, int matches, int wins)
    {
        return $"**Profile for {username}**\n" +
               $"- SteamID: {steamId}\n" +
               $"- Rating: {rating}\n" +
               $"- Matches: {matches}\n" +
               $"- Wins: {wins}";
    }

    internal static string FormatCreateLobby(string lobbyName, string lobbyId)
    {
        return $"**Lobby Created: {lobbyName}**\n" +
               $"- Lobby ID: {lobbyId}";
    }
}
