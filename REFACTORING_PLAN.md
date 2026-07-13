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

- [ ] **Purge committed secrets** (highest priority). `IHLDota2Bot/appsettings.json`
      is committed with real Steam account passwords (`Steam:BotAccount` +
      `Steam:PoolAccounts`) and a real `PlayerJwt:SigningKey` — despite the README's
      "empty placeholders only" policy. They entered history at `e64b427`. The repo
      is private, which limits exposure, but the JWT signing key lets anyone with
      repo access forge player tokens for the publicly tunnelled API.
      Steps: (1) move the values to user-secrets (mechanism already documented in
      the README), (2) commit the file with empty placeholders, (3) **rotate** the
      Steam passwords and the signing key (rotation matters more than history
      rewriting — treat the old values as burned), (4) optionally scrub history
      with `git filter-repo` if the repo may ever go public.
- [x] **Validate `returnUrl` in the Steam auth flow** (`AuthEndpoints`,
      `/steam/login` + `/steam/callback`). It was unvalidated, and the callback
      appends the freshly issued JWT to it — a crafted login link could exfiltrate
      a player's session token to an attacker's site (open redirect → token theft).
      Both endpoints now reject any `returnUrl` that is not an absolute http(s)
      URL on a loopback host (`IsAllowedReturnUrl`) with a 400 before redirecting
      or verifying; the callback validates first since it can be hit directly.
      Covered by `AuthReturnUrlValidationTests` (unit) and
      `SteamAuthReturnUrlTests` (integration).
- [ ] **Resolve the admin-panel vs. admin-key contradiction.** The Angular admin
      panel calls `/admin/*` with no `X-Api-Key` header, so any deployment that
      sets `GameManager:AdminApiKey` (as HOSTING_FOR_TESTING.md instructs) breaks
      the panel — which pressures operators to leave the gate open. Either have
      the dev-server proxy inject the key, or add a small key-entry field in the
      panel that stores it and sends it via an interceptor.
- [x] **MMR-balanced teams.** `LobbyPoolManager.HandleLobbyReadyAsync` previously
      assigned teams by random shuffle while the README advertised MMR balancing.
      Teams are now split by the pure `TeamBalancer` (stable sort by rating desc,
      greedy assign to the lower-sum team with size caps; input shuffled first so
      rating ties don't produce identical teams every match). Ratings come from a
      new `IPlayerRepository.GetByUserIdsAsync` batch lookup; missing rows fall
      back to the default rating. Covered by `TeamBalancerTests` (unit) and
      `LobbyPoolManagerTeamBalanceTests` (lobby-ready → persisted teams).
- [ ] **Minor hardening / consistency** (batchable):
      - `AdminApiKeyAuthenticationHandler`: compare keys with
        `CryptographicOperations.FixedTimeEquals` instead of `string.Equals`.
      - `PlayerRepository.GetRankAsync`: the `higherCount` predicate omits the
        username tie-break the leaderboard uses, so rank can disagree with
        leaderboard position on exact rating/wins/losses ties (comment claims
        parity).
      - `MeEndpoints.UserId` maps an unparsable `sub` claim to user id `0`
        instead of rejecting with 401.
      - Swagger is served through the tunnel in Development; consider disabling
        it for tunnelled test sessions or gating it.
      - `DiscordService.HandleMessageAsync` accepts `!` commands from any
        channel/DM the bot can read, not just `Discord:ChannelId` — confirm
        intentional or filter by channel.
      - `!register <steamId>` lets a user claim an arbitrary SteamId before its
        real owner registers (squatting). The unique index prevents duplicates;
        acceptable for a trust-based league, but worth an admin-visible audit log.

### Deferred (carried over from Phases 3 & 6)

- [ ] **Discord slash commands** (Discord.Net 3.x) to replace `!`-prefix parsing
      for discoverability and validation. The handler/dispatcher pipeline is
      transport-agnostic, so a slash-command front-end is additive — revisit as a
      dedicated change.
- [ ] **Frontend routing**: replace the inlined single page once a second view
      appears. Still a single view today.

## Suggested Next PR

The remaining top-priority item: move real credentials out of
`IHLDota2Bot/appsettings.json` into user-secrets, commit empty placeholders,
and rotate the burned values (Steam passwords + JWT signing key).
