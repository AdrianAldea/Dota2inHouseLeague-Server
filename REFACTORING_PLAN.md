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
