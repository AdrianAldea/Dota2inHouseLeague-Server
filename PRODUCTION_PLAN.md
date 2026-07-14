# Production Plan — Client-Only Players

**Goal:** in production, players interact with the league **only through the
desktop client**. Discord and the admin web panel become admin-only surfaces.

This plan came out of a full review of the desktop client flow (sign-in →
register → queue → ready → match creation) and the backend API surface it
talks to. Phases are ordered by risk: Phase 1 closes a security hole, Phases
2–3 close the functional/correctness gaps, Phases 4–5 are distribution and
posture.

---

## Review findings (summary)

**What already works well**

- Auth flow is production-grade: RFC 8252 loopback + Steam OpenID, server-side
  loopback-only `returnUrl` validation, JWT with 7-day expiry, DPAPI-encrypted
  token storage on Windows, auto-login with server-side validation.
- All player actions on the `me`-endpoints take identity from the JWT `sub`,
  never from a client-supplied id.
- Match results are auto-detected from the Dota GC (`MatchCompleted`), so the
  client doesn't need a reporting UI.

**Gaps found**

1. **(Security — fixed in Phase 1)** Every id-based mutation route under
   `/api/gamemanager` was unauthenticated: `POST /queue/{id}`, `/ready/{id}`,
   `/leave/{id}`, `/unready/{id}`, `/register`, `/unregister/{id}`,
   `/link/{id}/{steamId}`, `/match/report/{id}/{winner}`, `/challenge/*`,
   `/create`, `/invite`, plus `/api/server/start`. Since the API must be
   reachable by players' clients, any player could act **as any other player
   by id** — or make the bot create arbitrary Dota lobbies.
2. **(Functional — Phase 2)** The "match found" moment never reaches the
   client. Lobby name/password + team split are broadcast **only to Discord**;
   the client's banner is derived from ready-counts and disappears when the
   queue rolls over. A client-only player who misses the in-Dota invite can't
   find the lobby and never sees their team.
3. **(Correctness — Phase 3)** The client ignores `QueueOperationResponse.
   Success/Message` (a banned player clicking FIND MATCH sees "Joined the
   queue"); `InQueue` is optimistic client-side state that a restart loses;
   there is no UNREADY button; a mid-session 401 (token expiry) is not
   handled.
4. Minor: no `state` nonce on the loopback callback; token file is plaintext
   on macOS/Linux; 2-second polling could eventually become push (SignalR).

---

## Phase 1 — Lock down the backend API ✅

- [x] Move all id-based mutation routes behind the existing `X-Api-Key`
      authentication (the same gate as `/admin/*`): `register`,
      `unregister/{id}`, `link/{id}/{steamId}`, `queue/{id}`, `leave/{id}`,
      `ready/{id}`, `unready/{id}`, `match/report/{id}/{winner}`,
      `challenge/*`, `create`, `invite`, and `/api/server/start`.
      The Discord bot (`ApiKeyHandler`) and the admin panel
      (`apiKeyInterceptor`) already send the key on every request, so both
      keep working unchanged. Players are served by the JWT `me`-endpoints.
- [x] Keep public, id-free/read-only routes open: `queue/status`,
      `leaderboard`, `player/{id}`, `stats/{id}`, `rank/{id}`, `match/*`
      reads.
- [x] Add opt-in fixed-window per-IP rate limiting on the player surface
      (`/api/auth/*` and the `me`-endpoints). Disabled by default
      (`PlayerRateLimit:PermitLimit = 0`); enable in production, e.g.
      `PermitLimit = 60`, `WindowSeconds = 60`.
- [x] Integration tests: privileged routes 401 without key / work with key;
      public reads stay open; `me`-endpoints unaffected by the API key; rate
      limiter returns 429 past the limit and stays off by default.

**Production config checklist** (startup logs warn about each):

| Setting | Requirement |
|---|---|
| `GameManager:AdminApiKey` | long random string; same value in the Discord service (`SteamClient:ApiKey`) and pasted into the admin panel header |
| `PlayerJwt:SigningKey` | long random string (token validity across restarts) |
| `PlayerRateLimit:PermitLimit` | e.g. `60` per minute per IP |
| TLS | terminate at the edge (Cloudflare Tunnel already handled via forwarded headers) |
| `Swagger:Enabled` | `false` on any publicly reachable instance |

## Phase 2 — Surface the match in the client ✅

- [x] Backend: `GET /api/gamemanager/me/match` (JWT) — the signed-in player's
      current in-progress match: both rosters with sides plus the Dota lobby
      name + password. Credentials are derived from the lobby id via the shared
      `LobbyNaming` helper (same derivation the launch path uses), so nothing
      extra is persisted and only the match's participants can fetch them.
- [x] Client: "Match found" card on the Play screen — the 2s poll now also
      fetches `/me/match`; when a match exists the card shows your side,
      both team rosters (you highlighted), the lobby name/password (selectable
      for copy/paste), and join instructions. It stays until the match
      completes (the endpoint returns 404 and the card clears).

## Phase 3 — Client correctness fixes

- [ ] Respect `Success`/`Message` on every queue/ready/leave call and show the
      server's message (e.g. ban reason) instead of an unconditional success
      toast.
- [ ] Derive `InQueue` from the server so an app restart reconciles (include
      user ids in `queue/status`, or a small `/me/queue` flag).
- [ ] Add an UNREADY button (the API client already has `UnreadyAsync`).
- [ ] On any 401 mid-session, clear the session and return to the login screen.
- [ ] (Hardening) add a random `state` nonce to the loopback sign-in
      round-trip and verify it on the callback.

## Phase 4 — Distribute the client

- [ ] Ship `appsettings.json` with the production `ApiBaseUrl` next to the exe.
- [ ] Signed release builds for Windows/macOS/Linux (`dotnet publish` per RID).
- [ ] Pick an update mechanism (Velopack is the natural fit for Avalonia).
- [ ] First-run experience when the server is unreachable (instead of sample
      data with an "Offline" pill).

## Phase 5 — Admin-only posture for the other surfaces

- [ ] Discord: point `Discord:ChannelId` at an admins-only channel — the bot
      ignores every other channel, which makes it admin-only with zero code.
      Optional later: a config flag to disable player commands entirely.
- [ ] Admin panel: don't expose it publicly — host it on the internal network
      or behind an access proxy (e.g. Cloudflare Access). The `X-Api-Key`
      gate remains as the second layer.

## Later / nice-to-have

- SignalR (or similar) push for queue + match state instead of 2s polling.
- Encrypted token storage on macOS/Linux (Keychain / libsecret).
- Retire the `!` text commands and the `MessageContent` intent once admins are
  comfortable with slash commands (tracked in `REFACTORING_PLAN.md`).
