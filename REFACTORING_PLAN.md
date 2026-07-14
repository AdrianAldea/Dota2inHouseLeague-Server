# Refactoring & Improvements Plan — Dota 2 In-House League Bot

> Status: Phases 0–7 complete; remaining work below · Last updated: 2026-07-13
>
> Living roadmap. Check items off as they land. The original investigation and
> the completed phase details live in this file's git history (pre-2026-07-13
> revisions).

## Completed (Phases 0–7, for context)

Landed between 2026-06 and 2026-07 — see git history for specifics:

- **Foundations**: GitHub Actions CI (build/test/lint/format), frontend
  ESLint + Vitest, fixed Dockerfiles, `TreatWarningsAsErrors`, CSharpier.
- **Correctness**: eliminated `async void` handlers, Steam reconnect backoff
  with non-retryable-auth guard, DB-first match persistence with compensating
  rollback (`RollbackLaunchAsync`), stale-match reconciliation on startup.
- **Abstractions**: `IDota2Service`, `ISteamClient`, `ISteamGcClient(Pool)`,
  DI-registered `DiscordSocketClient`.
- **Discord restructure**: command-handler + dispatcher pipeline, declarative
  admin gating, centralized messages, per-user rate limiting, telemetry.
- **Resilience & observability**: OTel metrics/traces on both services, tuned
  HTTP resilience, classified Steam client errors.
- **Data & security**: EF Core migrations (with `EnsureCreated` baseline
  adoption), `X-Api-Key` admin authentication, secrets via user-secrets/env,
  configurable `League:PlayersPerLobby`.
- **Frontend hardening**: subscription teardown, no `any`, service/config
  layer, HTTP interceptor, accessibility pass, 16 unit tests.
- **Test depth**: 331 tests across endpoint edge cases, orchestration,
  ServiceDefaults, and Discord↔GameManager contract tests.

## Remaining Work

### Phase 8 — Security & Consistency (from the 2026-07-13 review)

Ordered by risk.

- [x] **Purge committed secrets.** All credentials moved from
      `IHLDota2Bot/appsettings.json` to user-secrets; the committed file now ships
      empty placeholders only, and the dead Steam-masking block was dropped from
      `appsettings.Development.json`. The `PlayerJwt:SigningKey` was **rotated**
      (fresh random key in user-secrets — the burned one no longer validates
      anything after the next server start). The integration-test factory now
      force-clears `SteamOptions` (`PostConfigure`) so tests stay hermetic and
      never pick up real credentials from user-secrets.
      **Operator action — done:** the Steam account passwords were changed on
      Steam's side and the new values are in place in user-secrets. Optionally
      scrub history with `git filter-repo` if the repo may ever go public.
- [x] **Validate `returnUrl` in the Steam auth flow** (`AuthEndpoints`,
      `/steam/login` + `/steam/callback`). It was unvalidated, and the callback
      appends the freshly issued JWT to it — a crafted login link could exfiltrate
      a player's session token to an attacker's site (open redirect → token theft).
      Both endpoints now reject any `returnUrl` that is not an absolute http(s)
      URL on a loopback host (`IsAllowedReturnUrl`) with a 400 before redirecting
      or verifying; the callback validates first since it can be hit directly.
      Covered by `AuthReturnUrlValidationTests` (unit) and
      `SteamAuthReturnUrlTests` (integration).
- [x] **Resolve the admin-panel vs. admin-key contradiction.** The admin panel
      header now has an **Admin API key** field (`AdminKeyService`, persisted to
      localStorage per browser) and an `apiKeyInterceptor` that attaches the value
      as `X-Api-Key` to every request — so setting `GameManager:AdminApiKey` no
      longer breaks the panel. Verified end-to-end in the browser; covered by
      `admin-key.service.spec` and `api-key.interceptor.spec`.
- [x] **MMR-balanced teams.** `LobbyPoolManager.HandleLobbyReadyAsync` previously
      assigned teams by random shuffle while the README advertised MMR balancing.
      Teams are now split by the pure `TeamBalancer` (stable sort by rating desc,
      greedy assign to the lower-sum team with size caps; input shuffled first so
      rating ties don't produce identical teams every match). Ratings come from a
      new `IPlayerRepository.GetByUserIdsAsync` batch lookup; missing rows fall
      back to the default rating. Covered by `TeamBalancerTests` (unit) and
      `LobbyPoolManagerTeamBalanceTests` (lobby-ready → persisted teams).
- [x] **Minor hardening / consistency** (landed as one batch):
      - `AdminApiKeyAuthenticationHandler` compares keys with
        `CryptographicOperations.FixedTimeEquals` (timing-safe).
      - `PlayerRepository.GetRankAsync` now applies the full leaderboard ordering
        (username tie-break included), so rank always matches the leaderboard row
        (`GetRank_ExactStatTies_AgreesWithLeaderboardOrder`).
      - The me-endpoints group rejects tokens whose `sub` doesn't parse with a
        401 endpoint filter instead of acting as user `0`
        (`MeAction_ValidlySignedTokenWithNonNumericSub_Returns401`).
      - Swagger can be disabled per session via `Swagger:Enabled=false`
        (documented in HOSTING_FOR_TESTING.md for public tunnel sessions).
      - `DiscordService` serves only the configured `Discord:ChannelId` when one
        is set (`ChannelPolicy`, mirrors `AdminPolicy`; 0 = accept anywhere).
      - `!register`/`!link` refuse claiming a SteamId already linked to another
        account, with a warning-level audit log of the attempt. This also aligns
        endpoint behavior with the DB's unique SteamId index — the old
        "both succeed" integration test only ever passed because the in-memory
        provider doesn't enforce unique indexes.

### Deferred (carried over from Phases 3 & 6)

- [x] **Discord slash commands** (Discord.Net 3.x). Landed as an additive
      front-end over the transport-agnostic handler/dispatcher pipeline: every
      `!` command has a slash equivalent (`SlashCommandCatalog`) dispatching to
      the same `IDiscordCommandHandler`, with option values mapped back onto the
      text-command `Parts`/mentions shape (`SlashCommandArgs`) so handlers work
      unchanged behind either front-end. Commands are bulk-registered on gateway
      Ready — guild-scoped when `Discord:GuildId` is set (instant), global
      otherwise — and the slash path shares the channel policy, admin gating,
      rate limiting, and telemetry of the text path. Interactions are deferred
      before invoking the handler (handlers can exceed Discord's 3-second
      acknowledge window) and "reply only on failure" handlers are closed out
      with a ✅ follow-up. Text `!` commands keep working. Covered by
      `SlashCommandCatalogTests` and `SlashCommandArgsTests`. Full guide with
      examples: `IHLDota2Bot.Discord/SLASH_COMMANDS.md`.
- [x] **Frontend routing**: the inlined admin page is gone — the app now
      bootstraps a `<router-outlet>` with `provideRouter` and lazy-loads the
      admin page at `/admin` (root and unknown paths redirect there). Adding a
      second view is now just another route entry.

## Status

All tracked refactoring work is complete. The `!`-prefix text commands were
kept alongside the new slash commands; retiring them (and the
`MessageContent` gateway intent they require) can be considered once the
league has fully switched to slash commands.
