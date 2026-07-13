using IHLDota2Bot.Discord.Enums;
using IHLDota2Bot.Discord.Helpers;

namespace IHLDota2Bot.Discord.Commands;

/// <summary>
/// `!commands` / `!help [command]` — lists every command, or shows detail for one.
/// The router maps the <c>!help</c> alias to <see cref="DiscordAction.COMMANDS"/>,
/// so this single handler serves both.
/// </summary>
public sealed class CommandsCommandHandler : IDiscordCommandHandler
{
    public DiscordAction Action => DiscordAction.COMMANDS;

    public async Task HandleAsync(IDiscordCommandContext context)
    {
        if (context.Parts.Length >= 2)
        {
            var lookup = context.Parts[1].TrimStart('!').ToLowerInvariant();
            if (CommandHelpText.TryGet(lookup, out var detail))
            {
                await context.ReplyAsync(detail);
                return;
            }

            await context.ReplyAsync(CommandMessages.NoHelpFor(lookup));
            return;
        }

        await context.ReplyAsync(CommandList);
    }

    private static readonly string CommandList = string.Join(
        "\n",
        [
            "**Available commands** — use `!help <command>` for details. Every command is also available as a slash command (`/queue`, `/help`, …).",
            "`!register <steamId>` — register your Steam account",
            "`!link <steamId>` — update your linked Steam ID",
            "`!unregister` — leave the league",
            "`!profile` — show your profile",
            "`!queue` / `!q` — join the matchmaking queue",
            "`!leave` — leave the queue",
            "`!ready` — mark yourself ready in the current lobby",
            "`!unready` — undo ready",
            "`!status` — show the current queue/lobby state",
            "`!mystats` — your rating / W-L",
            "`!stats @user` — stats for another player",
            "`!leaderboard [N]` — top N (default 10, max 50)",
            "`!match` — show your current active match",
            "`!matches [N]` / `!matches @user [N]` — recent completed matches (default 5)",
            "`!report radiant|dire` — report the winner of your current match",
            "`!challenge @user` — 1v1 duel invite (expires in 15 min)",
            "`!accept_match` — accept your latest pending challenge",
            "`!decline_match` — decline your latest pending challenge",
            "`!rank` / `!rank @user` — leaderboard rank + percentile",
            "`!score <matchId>` — show a finished match by id",
            "`!ping` — bot health and gateway latency",
            "`!version` — bot version + build sha",
            "`!commands` / `!help` — show this list (`!help <command>` for details)",
            "",
            "**Admin only**",
            "`!ban @user [reason]` · `!unban @user` · `!kick @user`",
            "`!force_start` · `!reset_queue` · `!pool_status`",
            "`!add_account <username> <password>` (delete the message after!)",
        ]
    );
}
