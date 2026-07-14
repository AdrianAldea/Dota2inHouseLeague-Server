# Discord Slash Commands — Guide

This bot understands **two ways of typing every command**:

1. **Text commands** — the classic way: type a message starting with `!`, e.g. `!queue`.
2. **Slash commands** — the modern Discord way: type `/` and pick the command
   from the menu Discord pops up, e.g. `/queue`.

Both do **exactly the same thing**. Slash commands are just nicer to use:
Discord shows you every available command while you type, tells you which
arguments it needs, and won't let you submit something malformed.

---

## 1. What it looks like for a player

### Example: joining the queue

| The old way | The new way |
|---|---|
| Type `!queue` and hope you spelled it right | Type `/` → Discord opens a menu of the bot's commands → pick **queue** → press Enter |

### Example: registering your Steam account

**Text command** — you have to know the format by heart:

```
!register 76561198012345678
```

**Slash command** — Discord asks you for each argument by name:

```
/register steamid: 76561198012345678
```

While typing, Discord literally shows a box labeled `steamid` with the hint
*"Your 17-digit Steam ID"*. You cannot send the command without filling it in,
because the argument is marked **required**.

### Example: looking up another player's stats

**Text command:**

```
!stats @Alice
```

**Slash command:**

```
/stats player: @Alice
```

The `player` argument is a real Discord **user picker** — you select the
person from a list, so no typos. It's also **optional**: sending `/stats`
with no player shows *your own* stats, same as `!stats` does.

### Example: reporting the winner of your match

**Text command** — typing `!report radiannt` (typo) just gets an error reply:

```
!report radiant
```

**Slash command** — the `winner` argument is a **choice list**. Discord shows
exactly two buttons, `radiant` and `dire`, and won't accept anything else:

```
/report winner: radiant
```

### Example: an admin banning a player

**Text command:**

```
!ban @Bob spamming the queue
```

**Slash command:**

```
/ban player: @Bob reason: spamming the queue
```

`player` is required, `reason` is optional. Non-admins who try it get an
ephemeral "not authorized" reply (only they can see it) — the same admin rules
as the text command, enforced by the same code.

### Small behavioral notes

- While the bot works on your command you'll see Discord's built-in
  *"IHLDota2Bot is thinking…"* indicator.
- Commands like `/queue` or `/ready` announce their result through the Game
  Manager broadcast in the league channel (same as the `!` versions). So that
  the interaction doesn't hang, the bot closes them out with a simple **✅**
  reply, and the broadcast message follows in the channel.
- Slash commands only work in the configured league channel (same
  `Discord:ChannelId` rule as text commands). Elsewhere you get an ephemeral
  "This command only works in the league channel."

---

## 2. Full command reference

Every slash command maps 1:1 to a text command and runs the *same handler*.

### Player commands

| Slash command | Arguments | Text equivalent | What it does |
|---|---|---|---|
| `/register` | `steamid` (required) | `!register <steamId>` | Link your Discord account to a Steam ID |
| `/link` | `steamid` (required) | `!link <steamId>` | Update your linked Steam ID |
| `/unregister` | — | `!unregister` | Leave the league |
| `/queue` | — | `!queue` / `!q` | Join the matchmaking queue |
| `/leave` | — | `!leave` | Leave the current lobby/queue |
| `/ready` | — | `!ready` | Mark yourself ready |
| `/unready` | — | `!unready` | Undo ready |
| `/status` | — | `!status` | Show queue/lobby state |
| `/profile` | — | `!profile` | Your profile |
| `/mystats` | — | `!mystats` | Your rating / W-L |
| `/stats` | `player` (optional) | `!stats @user` | Stats for a player (defaults to you) |
| `/leaderboard` | `count` (optional) | `!leaderboard [N]` | Top N players (default 10, max 50) |
| `/rank` | `player` (optional) | `!rank [@user]` | Leaderboard rank + percentile |
| `/match` | — | `!match` | Your current in-progress match |
| `/matches` | `count`, `player` (both optional) | `!matches [@user] [N]` | Recent completed matches |
| `/score` | `matchid` (required) | `!score <matchId>` | Show a finished match by ID |
| `/report` | `winner` (required: `radiant` or `dire`) | `!report radiant\|dire` | Report your match's winner |
| `/challenge` | `player` (required) | `!challenge @user` | 1v1 duel invite |
| `/accept_match` | — | `!accept_match` | Accept your pending challenge |
| `/decline_match` | — | `!decline_match` | Decline your pending challenge |
| `/ping` | — | `!ping` | Bot health / gateway latency |
| `/version` | — | `!version` | Bot version + build sha |
| `/help` | `command` (optional) | `!help [command]` / `!commands` | List commands, or detail for one |

### Admin commands

| Slash command | Arguments | Text equivalent |
|---|---|---|
| `/ban` | `player` (required), `reason` (optional) | `!ban @user [reason]` |
| `/unban` | `player` (required) | `!unban @user` |
| `/kick` | `player` (required) | `!kick @user` |
| `/force_start` | — | `!force_start` |
| `/reset_queue` | — | `!reset_queue` |
| `/add_account` | `username`, `password` (both required) | `!add_account <user> <pass>` |
| `/pool_status` | — | `!pool_status` |

> **Nice side effect for `/add_account`:** slash-command arguments are only
> visible to the person typing them, so the Steam password never appears as a
> channel message. (The text version has to delete your message afterwards to
> scrub it.)

---

## 3. Setup

### a) Invite the bot with the right scope

Slash commands require the **`applications.commands`** OAuth2 scope. When
generating the bot invite URL in the Discord Developer Portal, tick **both**:

- `bot`
- `applications.commands`

If the bot was invited with only the `bot` scope, slash commands will not
appear — re-invite it with both scopes (no need to kick it first, just open
the new invite URL).

### b) Choose where commands are registered: `Discord:GuildId`

The bot registers its commands automatically on startup. Where it registers
them is controlled by one setting:

```jsonc
// appsettings.json (placeholder — set the real value in user-secrets)
"Discord": {
  "Token": "",
  "ChannelId": 0,
  "GuildId": 0,   // ← this one
  ...
}
```

| Value | Meaning |
|---|---|
| your server's ID (e.g. `123456789012345678`) | Commands are registered **only in that server** and appear **instantly**. Recommended — an in-house league lives in one server anyway. |
| `0` (default) | Commands are registered **globally** (every server the bot is in). Discord can take **up to an hour** to distribute them. |

Set it the same way as the other secrets:

```bash
cd IHLDota2Bot.Discord
dotnet user-secrets set "Discord:GuildId" "123456789012345678"
```

(To find your server's ID: Discord → User Settings → Advanced → enable
*Developer Mode*, then right-click the server icon → *Copy Server ID*.)

### c) Nothing else

There is no separate registration script or deploy step. On every startup the
bot bulk-overwrites its command list, so the commands Discord shows always
match the code that is running.

---

## 4. How it works internally (for developers)

The bot's command logic lives in **handlers** (`Commands/*CommandHandlers.cs`),
one per action, behind the `IDiscordCommandHandler` interface. Handlers don't
know or care whether the command arrived as a text message or a slash
interaction — they receive an `IDiscordCommandContext` with:

- `Parts` — the command split into words, e.g. `["ban", "<@42>", "spamming"]`
- `MentionedUserIds` — any users referenced
- `ReplyAsync(...)` — how to answer

The slash front-end's whole job is to **translate a slash interaction into
that same shape**, then reuse everything that already exists.

### Worked example: `/ban player: @Bob reason: spamming the queue`

Suppose Bob's Discord ID is `42`. Step by step:

1. **Discord sends an interaction** to the gateway; `DiscordService`
   receives it in `HandleSlashCommandAsync`.
2. **Catalog lookup** — `SlashCommandCatalog` (`Commands/SlashCommandCatalog.cs`)
   is the single table declaring every slash command: its name, description,
   arguments, and which `DiscordAction` it maps to. `"ban"` maps to
   `DiscordAction.BAN`, whose registered handler is `BanCommandHandler` —
   the *same* handler that serves `!ban`.
3. **Pipeline checks** — the same ones text commands go through:
   channel policy (`ChannelPolicy`), admin gating (`AdminPolicy`, because
   `BanCommandHandler.RequiresAdmin` is true), rate limiting, telemetry.
4. **Defer** — the bot immediately answers "thinking…" (`DeferAsync`),
   because handlers call the Game Manager API and may need more than
   Discord's 3-second response deadline.
5. **Argument translation** — `SlashCommandArgs.Build` converts the typed
   options into the text-command shape:

   | | text command `!ban @Bob spamming the queue` | slash command `/ban player:@Bob reason:spamming the queue` |
   |---|---|---|
   | `Parts` | `["ban", "<@42>", "spamming", "the", "queue"]` | `["ban", "<@42>", "spamming the queue"]` |
   | `MentionedUserIds` | `[42]` | `[42]` |

   Note the `player` option contributes *both* the mention token in `Parts`
   *and* the entry in `MentionedUserIds` — exactly what an inline `@Bob`
   produces — so `BanCommandHandler`'s existing parsing
   (`FirstMentionedUserId` for the target, `Parts[2..]` joined for the
   reason) works unchanged for both rows of that table.
6. **Handler runs** — `BanCommandHandler.HandleAsync(context)` calls the Game
   Manager exactly as it does for `!ban`. Its replies go through
   `SlashCommandContext.ReplyAsync`, which sends interaction follow-ups.
7. **Close-out** — mutating commands reply only on failure (success is
   announced by the Game Manager broadcast). If the handler didn't reply,
   the pipeline sends **✅** so the deferred interaction doesn't hang.

### File map

| File | Role |
|---|---|
| `Commands/SlashCommandCatalog.cs` | Declares every slash command (name, description, options, target action). **Adding a new command's slash version = adding one entry here.** |
| `Commands/SlashCommandArgs.cs` | Pure function: catalog definition + provided option values → (`Parts`, `MentionedUserIds`) |
| `Commands/SlashCommandContext.cs` | `IDiscordCommandContext` over a `SocketSlashCommand`; replies = follow-ups |
| `Services/DiscordService.cs` | Registers the catalog on gateway Ready (`RegisterSlashCommandsAsync`) and dispatches interactions (`HandleSlashCommandAsync`) |
| `..Tests/SlashCommandCatalogTests.cs` | Pins Discord's structural rules (name format, description length, required-before-optional) |
| `..Tests/SlashCommandArgsTests.cs` | Pins the `Parts`/mentions mapping per argument kind |

### Adding a slash version of a new command

1. Implement the command as a normal `IDiscordCommandHandler` and register it
   in `Program.cs` (this is unchanged — it also gives you the `!` version).
2. Add its `DiscordAction` to the enum and one `SlashCommandDefinition` entry
   to `SlashCommandCatalog.Definitions`, e.g.:

   ```csharp
   new(
       DiscordAction.WHATEVER,
       "whatever",
       "One-line description shown in the Discord menu",
       new SlashCommandOptionDefinition("target", "Who to affect", UserOpt, true),
       new SlashCommandOptionDefinition("note", "Optional note", StringOpt, false)
   ),
   ```

   Declare options in the order the text command expects them in `Parts`,
   and required options before optional ones (a Discord rule — there's a test
   for it).
3. Done — registration on startup and dispatch are driven by the catalog.

---

## 5. Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| Typing `/` shows no commands from the bot | Bot invited without the `applications.commands` scope → re-invite with it. Or `GuildId` is `0` and global propagation (up to 1 h) isn't done → set `Discord:GuildId` for instant registration. |
| Commands appear but "The application did not respond" | The bot process isn't running / can't reach the gateway. Check the bot logs. |
| "This command only works in the league channel." | You used the command outside the configured `Discord:ChannelId`. |
| "Not authorized." | The command is admin-only; your user/role isn't in `Discord:AdminUserIds` / `Discord:AdminRoleName`. |
| "Slow down — try again in Ns." | Per-user rate limit (same limiter as text commands). |
| Changed the catalog but Discord still shows old commands | Restart the bot (registration happens on startup); with `GuildId = 0` allow up to an hour for global propagation. |
