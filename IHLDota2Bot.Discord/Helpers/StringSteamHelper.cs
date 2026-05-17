using Discord;
using Discord.WebSocket;

namespace IHLDota2Bot.Discord.Helpers;

public static class StringSteamHelper
{
    public static Optional<ulong> GetSteamIdIfValidMessageAsync(string[] message_list)
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
            Console.WriteLine($"{nameof(GetSteamIdIfValidMessageAsync)}: " + steamId);
            return Optional<ulong>.Unspecified;
        }

        return steamId;
    }
}
