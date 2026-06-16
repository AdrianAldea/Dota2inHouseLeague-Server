# Refactoring & Improvements Plan — Dota 2 In-House League Bot

> Status: Proposed · Last updated: 2026-06-15
>
> This document captures the results of a full-solution investigation and a
> prioritized, phased plan to improve code quality, testability, resilience,
> and operability. It is intended as a living roadmap — check items off as
> they land.

## 1. Scope of Investigation

The solution is a .NET Aspire–orchestrated, multi-project system:

| Project | Role | Approx. size |
| --- | --- | --- |
| `IHLDota2Bot` | Game Manager Web API (minimal APIs, EF Core, SteamKit2) | ~47 files / ~6.5k LOC |
| `IHLDota2Bot.Discord` | Discord bot service (Discord.Net 3.20.1) | ~12 files |
| `IHLDota2Bot.Common` | Shared DTOs / requests / enums | ~15 files |
| `IHLDota2Bot.ServiceDefaults` | OpenTelemetry, resilience, health checks | small |
| `IHLDota2Bot.AppHost` | Aspire orchestration | small |
| `IHLDota2BotFrontEnd` | Angular 21 admin dashboard | ~482 LOC, 0 tests |
| `*.Tests` / `IntegrationTests` | xUnit + Moq, in-memory EF, WebApplicationFactory | partial |

**Healthy baseline:** all C# projects target **.NET 10** with **nullable
reference types enabled**; the domain layer (LobbyQueue, EloCalculator,
SteamAccountPool) is pure and well-tested; repository pattern and the Options
pattern are used; integration tests exercise the full match lifecycle in
memory; **no TODO/FIXME/HACK debt** was found in C#.

## 2. Top Findings (Cross-Cutting)

These are the highest-impact issues, ordered by risk.

1. **No CI/CD.** There is no `.github/workflows` directory — no automated
   build, test, or lint on push/PR. This is the single biggest gap: every
   refactor below is riskier without a green-build gate.
2. **Broken/stale Docker.** `IHLDota2Bot/Dockerfile` references **.NET 8
   Windows nanoserver**, a non-existent project path (`Dota2Bot/`), and
   Windows-only `%BUILD_CONFIGURATION%` syntax. `IHLDota2Bot.Discord/Dockerfile`
   is correct (.NET 10 Linux). No `docker-compose.yml`.
3. **`async void` event handlers (backend).** ~9 `async void` handlers in
   `LobbyPoolManager` — unobserved exceptions can crash the process.
4. **Monolithic `DiscordService` (732 LOC).** A 94-line switch dispatches 35
   commands; validation, formatting, and dispatch are interleaved; **0 unit
   tests** cover it.
5. **Missing service abstractions.** No `IDota2Service`, no `ISteamClient`, no
   Discord command-handler interface — endpoints/handlers bind to concrete
   types, blocking unit testing and substitution.
6. **No HTTP resilience at call sites.** ServiceDefaults adds the standard
   resilience handler, but Discord/Steam HTTP calls have no explicit timeout,
   retry, or circuit-breaker semantics and swallow exceptions returning `null`.
7. **Frontend: subscription leaks + `any` types + zero tests.** Manual
   `.subscribe()` without teardown (auto-refresh every 5s), `any` in
   `CardComponent`, no ESLint, no Vitest config despite tsconfig referencing it.
8. **Schema via `EnsureCreated`, not migrations.** No versioned schema or
   rollback path; the README documents `dotnet ef database update` which won't
   work as written.
9. **Server-side authorization is a TODO.** Admin endpoints rely on the Discord
   layer to gate by user id/role (`AdminEndpoints.cs`); the API itself is open.
10. **Config/docs drift.** README claims .NET 8 / Angular 21 / SQL Server with
    EF migrations and Swagger at `:5000`; reality is .NET 10, `EnsureCreated`,
    and Aspire-assigned ports. `.gitmodules` is empty (no-op, should be removed).

## 3. Phased Plan

Each phase is independently shippable. Recommended order maximizes safety
(get the build gate first) before larger refactors.

### Phase 0 — Foundations & Safety Net (do first)

Goal: make every later change verifiable and reversible.

- [x] **Add GitHub Actions CI**: restore, build, `dotnet test` (whole solution),
      and Angular `npm ci` + lint + test + build on push/PR to `main`.
      (`.github/workflows/ci.yml`)
- [x] **Add frontend lint/test tooling**: ESLint (`angular-eslint` flat config)
      + the Angular Vitest unit-test builder with a smoke test; `npm test` and
      `npm run lint` wired into CI. Known pre-existing lint findings
      (accessibility, `any`, `inject()`) are surfaced as warnings and tracked in
      Phase 6 so the gate stays green.
- [x] **Fix the backend Dockerfile** (`IHLDota2Bot/Dockerfile`): .NET 10 Linux
      base, correct project/restore paths, POSIX build-arg syntax; flipped
      `DockerDefaultTargetOS` to `Linux`. Multi-service orchestration is left to
      Aspire (documented in the README) rather than a hand-maintained compose
      file.
- [x] **Reconcile README with reality**: .NET 10, Aspire run instructions,
      `EnsureCreated` bootstrap note, Aspire-assigned ports/Swagger, `npm test`/
      `npm run lint`, accurate Docker guidance.
- [x] **Remove the empty `.gitmodules`**.
- [ ] Consider `TreatWarningsAsErrors` (or a ruleset) once the build is clean.

### Phase 1 — Correctness & Safety (backend)

Goal: eliminate crash/race risks.

- [x] **Replace `async void` handlers** in `LobbyPoolManager`: the ten LobbyQueue
      event handlers now dispatch through a single fault-observing wrapper
      (`RunHandlerSafely`/`ObserveHandlerAsync`) — no `async void`, faults are
      logged instead of crashing the process. (The two self-detaching inline GC
      lambdas in the lobby-ready path already wrap their awaited work in
      try/catch and are left as-is.)
- [x] **Audit thread-safety in `SteamGcClient`**: documented the single-threaded
      callback-pump model in the class remarks and marked the cross-thread
      `bool` flags (`isRunning`, `isLoggedOn`, `hasActiveLobby`,
      `matchResultReported`) `volatile` for cross-thread visibility.
- [x] **Harden reconnection**: replaced the fixed 5s delay with exponential
      backoff (5s → 2min cap, reset on successful logon) and a non-retryable
      auth guard (bad password / SteamGuard / disabled / banned) that stops the
      reconnect loop instead of hammering Steam with credentials that can't work.
- [~] **Match/Steam consistency**: the lobby-ready path already persists the
      match in the DB *first* and rolls back the in-memory reservation + pool
      account if the write fails (so a DB failure can't strand a lobby), and
      both the GC `MatchCompleted` path and admin destroy settle the DB row
      idempotently. Left as-is; revisit if a transactional boundary across the
      DB and Steam GC is needed later.

### Phase 2 — Abstractions & Testability

Goal: unlock unit testing and substitution.

- [x] **Extract `IDota2Service`**; all Game Manager endpoints now depend on the
      interface (`AddSingleton<IDota2Service, Dota2Service>`).
- [x] **Extract `ISteamClient`** (Discord side); `DiscordService` depends on the
      interface, and the 25 API paths are centralized in a `GameManagerApiRoutes`
      helper instead of inline string literals.
- [x] **Wrap the Dota GC client** behind `ISteamGcClient` (implemented by
      `SteamGcClient`); `SteamGcClientPool` and its consumers work through the
      interface, keeping the SteamKit2/protobuf details and constants
      (AppId 570, lobby type 2004) isolated behind it.
- [x] **Register `DiscordSocketClient` via DI** instead of `new`-ing it inside
      `DiscordService`, so the service can be constructed/tested with an injected
      gateway client.

### Phase 3 — Discord Bot Restructure

Goal: dismantle the 732-line monolith.

- [x] **Command-handler pattern (3a — read-only commands)**: introduced
      `IDiscordCommandHandler`, `DiscordCommandDispatcher`, and
      `DiscordCommandContext`; migrated all 12 read-only/info commands
      (PING, VERSION, STATUS, PROFILE, MYSTATS, STATS, LEADERBOARD, RANK,
      MATCH, MATCHES, SCORE, POOL_STATUS) out of the monolithic switch into
      individual handler classes. The dispatcher short-circuits before the
      switch — zero behavior change for unmigrated commands (incremental
      migration strategy).
- [x] **Command-handler pattern (3b — state-changing commands)**: migrated the
      remaining commands (REGISTER, LINK, UNREGISTER, QUEUE, LEAVE, READY,
      UNREADY, REPORT, CHALLENGE, ACCEPT_MATCH, DECLINE_MATCH, BAN, UNBAN, KICK,
      FORCE_START, RESET_QUEUE, ADD_ACCOUNT, COMMANDS) into handlers and
      **deleted the legacy switch**. `DiscordService` is now a thin router
      (parse → resolve handler → gate → invoke) and no longer depends on
      `ISteamClient`. Handlers consume an `IDiscordCommandContext` abstraction so
      they're unit-testable without the Discord gateway.
- [x] **Cross-cutting pipeline**: admin gating is now declarative
      (`IDiscordCommandHandler.RequiresAdmin`) and enforced once in the router
      pipeline via the pure, testable `AdminPolicy`; per-command logging stays in
      the pipeline. (Rate limiting is tracked with Phase 4, where it also appears.)
- [x] **Centralize messages**: all usage/error strings live in `CommandMessages`;
      the two `ReplyIfFailedAsync` overloads collapsed into one
      `CommandReplies.ReplyIfFailedAsync`, and the duplicated mention-validation
      now flows through `IDiscordCommandContext.FirstMentionedUserId`.
- [x] **Add `IsAdmin`/authorization tests** and command-dispatch tests:
      `AdminPolicyTests`, `DiscordCommandDispatcherTests` (routing + duplicate
      registration), and `CommandHandlerTests` (mock `ISteamClient` + fake
      context) — Discord test count 39 → 74.
- [ ] **Evaluate slash commands** (Discord.Net 3.x) to replace `!`-prefix
      parsing for discoverability and validation. *(Deferred: the handler/
      dispatcher pipeline is now transport-agnostic, so adding a slash-command
      front-end later is additive — revisit as a dedicated change.)*

### Phase 4 — Resilience & Observability

Goal: production-grade external-call behavior and insight.

- [x] **Rate limiting** on Discord commands (4a): per-user sliding-window
      `CommandRateLimiter` (configurable via `Discord:RateLimit`, default 5
      commands / 5s, disabled at 0). Enforced in the router pipeline; admin
      commands bypass it. Backed by `TimeProvider` for deterministic tests.
- [x] **Custom metrics & tracing**: both sides instrumented and surfaced through
      the existing OpenTelemetry pipeline.
      *Discord (4a):* `DiscordCommandTelemetry` — `discord.commands.handled`
      counter (tagged command + outcome: handled/failed/denied/rate_limited),
      `discord.commands.duration` histogram, and a per-command `ActivitySource`
      span; the router now catches/observes handler exceptions instead of letting
      them escape to the gateway.
      *Backend (4b):* `GameMetrics` — `ihl.lobbies.created` / `ihl.matches.created`
      / `ihl.matches.finished` (tagged outcome) counters, an `ihl.matches.duration`
      histogram (lobby-ready → finish), and an `ihl.queue.depth` observable gauge.
- [x] **Explicit HTTP policies** (4c): the Aspire standard resilience handler
      (timeout + retry-with-jitter + circuit breaker) already applies to every
      client via ServiceDefaults; the `SteamClient` now tunes it for interactive
      use via named `HttpStandardResilienceOptions` (5s per-attempt, 15s total,
      2 retries). Failures are no longer swallowed undifferentiated — a
      `SteamClientErrorClassifier` sorts them (timeout / open-circuit / cancelled /
      transport / unexpected) so transient conditions log as warnings and only
      real faults log as errors, while preserving the "null means failure" contract.

### Phase 5 — Data & Security

- [x] **Adopt EF Core migrations** in place of `EnsureCreated`; add an initial
      migration and wire `DatabaseInitializer` to migrate. Legacy `EnsureCreated`
      databases are auto-baselined (`SchemaUpgradePlanner`); non-relational
      providers (in-memory tests) fall back to `EnsureCreated`.
- [x] **Server-side authorization** on admin API endpoints (don't rely solely on
      the Discord layer). `AdminApiKeyAuthenticationHandler` validates `X-Api-Key`
      header; `ApiKeyHandler` injects it from the Discord service. Key flows via
      user-secrets / env var; unconfigured = warning + open (dev ergonomics).
- [ ] **Verify `PlayersPerLobby`** (currently 2) is intentional vs. a 5v5/test
      value; surface league sizing via config.
- [ ] **Secrets review**: confirm all tokens/keys flow through user-secrets/env;
      avoid plaintext credential paths (e.g. `add_account`).

### Phase 6 — Frontend Hardening

- [ ] **Fix subscription leaks**: `takeUntil(destroy$)` (or `toSignal`/async
      pipe) on every HTTP call in `LobbyMonitorComponent` and `CardComponent`.
- [ ] **Remove `any`**: generics/interfaces in `CardComponent`; type all responses.
- [ ] **Service + config layer**: extract API calls and the hardcoded
      `/discord` and `/gamemanager` endpoints into services with environment
      config; add a global `HttpInterceptor` for errors/timeouts.
- [ ] **Tests**: start with services and `LobbyMonitor` logic (Vitest).
- [ ] **Accessibility pass**: ARIA labels, `label[for]` associations, focus
      states, semantic landmarks; move the one inline style to CSS.
- [ ] **Routing**: replace the inlined single page once a second view appears.

### Phase 7 — Test Depth (cross-cutting, ongoing)

- [ ] Endpoint integration tests for `MatchEndpoints`/`PlayerEndpoints` beyond
      the happy path (error/edge cases).
- [ ] Tests for `Dota2Service` and `LobbyPoolManager` orchestration.
- [ ] Functional tests for ServiceDefaults (resilience/OTel) beyond smoke tests.
- [ ] Contract tests for Discord ↔ Game Manager HTTP boundary.

## 4. Quick Wins (low effort, high value)

These can land immediately, mostly independent of the phases above:

- Delete empty `.gitmodules`.
- Fix `IHLDota2Bot/Dockerfile`.
- Add CI build+test workflow.
- Dedupe `ReplyIfFailedAsync` overloads and the repeated mention-validation block.
- Centralize the "You are not registered." message and the 25 API path strings.
- Add `takeUntil` teardown to the two leaking frontend subscriptions.
- Correct the README's framework versions and run instructions.

## 5. Risks & Sequencing Notes

- **Do Phase 0 first.** Large refactors (Phases 2–3) without CI are high-risk.
- The Discord restructure (Phase 3) depends on the abstractions in Phase 2.
- Migrations (Phase 5) is a behavioral change to startup — land it with care and
  a tested upgrade path from the current `EnsureCreated` databases.
- Frontend work (Phase 6) is fully parallelizable with backend work.

## 6. Suggested First PR

A focused, low-risk starter that unblocks everything else:

1. GitHub Actions CI (build + test for .NET and Angular).
2. Fix the backend Dockerfile.
3. Remove `.gitmodules`.
4. README accuracy fixes.

This establishes the safety net and clears the most obvious drift before any
behavioral refactoring begins.
