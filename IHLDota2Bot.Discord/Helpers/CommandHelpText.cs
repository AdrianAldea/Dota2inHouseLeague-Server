namespace IHLDota2Bot.Discord.Helpers;

/// <summary>
/// Lookup for `!help &lt;command&gt;` detail text. Keyed by the bare command
/// (no leading `!`), case-insensitive.
/// </summary>
public static class CommandHelpText
{
    private static readonly Dictionary<string, string> Entries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["register"] = "**!register <steamId>**\nLinks your Discord account to a 17-digit Steam ID. Re-running it updates the row but preserves rating and W/L.",
        ["link"] = "**!link <steamId>**\nUpdates the Steam ID on your existing registration. Must be registered first.",
        ["unregister"] = "**!unregister**\nRemoves your row from the database and pulls you out of the current queue.",
        ["profile"] = "**!profile**\nAlias for `!mystats` — shows your own rating + W/L.",
        ["queue"] = "**!queue** (alias `!q`)\nJoins the matchmaking queue. Refused if you're banned or already in a lobby.",
        ["q"] = "**!q**\nAlias for `!queue`.",
        ["leave"] = "**!leave**\nRemoves you from the current lobby. Clears your ready flag.",
        ["ready"] = "**!ready**\nMarks you ready in your current lobby. When the whole lobby is ready, a match is auto-created with a random 5/5 team split.",
        ["unready"] = "**!unready**\nFlips your ready state back to not-ready.",
        ["status"] = "**!status**\nLists the current lobby roster with ready markers and counts of registered/waiting players.",
        ["mystats"] = "**!mystats**\nYour rating, W-L, win-rate.",
        ["stats"] = "**!stats @user**\nStats for the first mentioned user. Falls back to you if no mention.",
        ["leaderboard"] = "**!leaderboard [N]**\nTop N by rating desc → wins desc → losses asc → username. Default 10, max 50.",
        ["rank"] = "**!rank** / **!rank @user**\nShows the leaderboard rank and percentile.",
        ["match"] = "**!match**\nShows your current in-progress match — teams, your side, and the match id.",
        ["matches"] = "**!matches [N]** / **!matches @user [N]**\nLast N completed matches (default 5, max 20).",
        ["report"] = "**!report radiant|dire**\nReports the winner of your current in-progress match. Applies ELO and W/L to all 10 participants.",
        ["score"] = "**!score <matchId>**\nShow a finished match by its UUID.",
        ["challenge"] = "**!challenge @user**\n1v1 duel invite. Expires in 15 minutes. Cancels any prior outgoing pending challenge from you.",
        ["accept_match"] = "**!accept_match**\nAccept your latest pending incoming challenge. A 1v1 match is created (you = Dire, challenger = Radiant).",
        ["decline_match"] = "**!decline_match**\nDecline your latest pending incoming challenge.",
        ["ping"] = "**!ping**\nReplies with the Discord gateway latency in milliseconds.",
        ["version"] = "**!version**\nBot assembly version and the GIT_COMMIT env var, if set at deploy time.",
        ["commands"] = "**!commands** (alias `!help`)\nLists every command. Pass an argument for details: `!help queue`.",
        ["help"] = "**!help [command]**\nLists every command, or shows details for one.",
        ["ban"] = "**!ban @user [reason]** *(admin)*\nBans the user from queueing. Also removes them from the current lobby. Reason is stored.",
        ["unban"] = "**!unban @user** *(admin)*\nClears the ban.",
        ["kick"] = "**!kick @user** *(admin)*\nRemoves the user from their current lobby. They stay registered.",
        ["force_start"] = "**!force_start** *(admin)*\nMarks everyone in the current lobby ready and fires LobbyReady (creating a match).",
        ["reset_queue"] = "**!reset_queue** *(admin)*\nEmpties the current lobby and releases all players back to waiting.",
        ["add_account"] = "**!add_account <username> <password>** *(admin)*\nAdds a Steam account to the pool. The bot deletes the original Discord message after to scrub the password.",
        ["pool_status"] = "**!pool_status** *(admin)*\nLists every Steam pool account, its state, and current lobby."
    };

    public static bool TryGet(string command, out string detail) => Entries.TryGetValue(command, out detail!);
}
