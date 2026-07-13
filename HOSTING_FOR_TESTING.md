# Hosting the API locally for real-world testing (router port-forwarding + port bridge)

How to expose the Game Manager API straight from your PC so other players can connect over
the internet during a test session. No third-party tunnel, no cloud, no cost.

Fits an "open → test for ~1 hour → close" workflow: the API holds persistent Steam bot
connections while it runs, so it only needs to be up during the session.

**Exposure method:** router port-forward → Windows firewall rule → a `netsh portproxy`
bridge that forwards the public-facing port to Kestrel's loopback binding. (This is
"Method A". An earlier "Method B" that bound Kestrel to all interfaces directly was
removed — do not reintroduce it, it collides with the bridge.)

---

## Your environment (as detected 2026-07-13)

| Thing | Value | Notes |
|---|---|---|
| PC LAN IP (router forwards here) | `192.168.1.10` | The "Ethernet" adapter — the router port-forward target. |
| Public IP (players connect here) | `82.76.59.60` | **Dynamic** — can change when the router/ISP reconnects. See DDNS note. |
| API internal port | `5193` | Fixed by the AppHost change (`IHLDota2Bot.AppHost/Program.cs`). |
| External port | *the port you opened on the router* | May or may not equal 5193 — see Step 1. |
| IP Helper service (`iphlpsvc`) | Running | Required for `netsh portproxy`. Confirmed present. |

Throughout: replace `<EXTERNAL_PORT>` with the port you forwarded on the router, and
`82.76.59.60` with your current public IP.

---

## The core problem this solves

Your router forwards `82.76.59.60:<EXTERNAL_PORT>` → `192.168.1.10:5193`. But Kestrel (the
API's web server) binds to **`localhost` (127.0.0.1) only** by default, so it rejects any
connection that didn't originate from the PC itself. Forwarded traffic arrives on the
`192.168.1.10` interface and gets refused.

The fix: a `netsh portproxy` rule that listens on **all** interfaces at `5193` and relays to
`127.0.0.1:5193` where Kestrel is listening — plus a Windows Firewall rule to let the inbound
traffic in.

```
Player ──internet──▶ 82.76.59.60:<EXTERNAL_PORT>
        (router port-forward)
                 └──▶ 192.168.1.10:5193  ──▶ portproxy 0.0.0.0:5193
                                                      └──▶ 127.0.0.1:5193 (Kestrel)
```

---

## Prerequisites

- **Docker Desktop** — the AppHost provisions the SQL Server container.
- **.NET SDK** — to run the AppHost.
- **An elevated (Administrator) PowerShell** — for the firewall + portproxy rules. Open with
  Win+X → **Terminal (Admin)** / **Windows PowerShell (Admin)**.

---

## Part 1 — One-time setup (do these once, ever)

### Step 1. Confirm the router forward
You've already configured this. Just confirm it maps an external port to
**`192.168.1.10` port `5193` (TCP)**:
- If your external port is also `5193`, players use `http://82.76.59.60:5193`.
- If it's something else (e.g. `8080`), players use `http://82.76.59.60:8080` — everything
  still works, because the public port is what ends up in the URL and the OpenID realm.

### Step 2. Add the firewall rule + port bridge (elevated PowerShell)
Run this whole block in an **Administrator** PowerShell. It's **idempotent** — safe to re-run.

```powershell
# 1) Firewall: allow inbound TCP 5193 (remove any prior rule of the same name first)
Get-NetFirewallRule -DisplayName "IHL API 5193" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -DisplayName "IHL API 5193" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5193 | Out-Null

# 2) Port bridge: expose 5193 on all interfaces -> Kestrel's loopback
#    (delete-then-add makes it idempotent)
netsh interface portproxy delete v4tov4 listenaddress=0.0.0.0 listenport=5193 2>$null
netsh interface portproxy add    v4tov4 listenaddress=0.0.0.0 listenport=5193 connectaddress=127.0.0.1 connectport=5193

# 3) Show the results
Write-Host "`n--- portproxy table ---"
netsh interface portproxy show v4tov4
Write-Host "`n--- firewall rule ---"
Get-NetFirewallRule -DisplayName "IHL API 5193" | Select-Object DisplayName, Enabled, Direction, Action | Format-Table -AutoSize
```

**Expected output:** a portproxy row `Address 0.0.0.0  Port 5193  →  127.0.0.1  5193`, and a
firewall rule showing `Enabled=True, Direction=Inbound, Action=Allow`. Both the firewall rule
and the bridge **persist across reboots**, so this is genuinely one-time.

### Step 3. Set the admin API key
Admin endpoints are unrestricted until this is set (the API logs a warning about it at
startup). Set it before exposing the server publicly — no elevation needed:
```powershell
dotnet user-secrets --project IHLDota2Bot set "GameManager:AdminApiKey" "<choose-a-secret>"
```

### Step 4. Per-tester: point each client at your public address (each player, once)
On **each player's** machine:
```powershell
setx IHL_API_BASE "http://82.76.59.60:<EXTERNAL_PORT>"
```
> `setx` writes the variable for **future** processes — the player must open a **new**
> terminal (or restart the desktop client) for it to take effect; it does not affect an
> already-open shell.

Then they launch the desktop client normally. Steam login works: the client opens a local
callback on the player's own machine, the server bounces the browser back to it, and the
OpenID realm is built from the public address the player used.

> If your public IP changes, testers must update this value. A DDNS hostname (see Security &
> reliability) avoids that.

---

## Part 2 — Every test session

### Step 1. Start Docker Desktop
Make sure the Docker engine is up so the AppHost can start the SQL Server container.

### Step 2. Start the full stack
```powershell
dotnet run --project IHLDota2Bot.AppHost
```
This brings up SQL Server (container, port 52001), the Game Manager API on **5193**, and the
Steam bot accounts (they log in on boot). Wait in the Aspire dashboard (opens automatically)
until `Dota2inHouseLeague-Backend` shows **Running/Healthy**.

> The Discord bot and Angular frontend also start by default; they aren't needed for API
> testing. Ignore them, or Stop those two resources from the Aspire dashboard.

### Step 3. Confirm Kestrel is on loopback and the bridge is up
```powershell
netstat -ano | Select-String ":5193.*LISTENING"
```
**Expected:** two lines —
- `127.0.0.1:5193` → Kestrel (owned by the `dotnet` process), and
- `0.0.0.0:5193` → the portproxy bridge.

> **If you instead see only `0.0.0.0:5193` owned by the `dotnet` process**, Aspire is binding
> Kestrel to all interfaces itself and the bridge is redundant — you can delete the portproxy
> rule (see Teardown) and rely on the firewall rule alone. With the current code the expected
> case is loopback + bridge.

### Step 4. Verify reachability
- **From this PC:** open `http://localhost:5193/swagger` — Swagger loads (the app runs in the
  Development environment).
- **From outside your home network:** on a phone using **mobile data (Wi-Fi off)**, browse
  `http://82.76.59.60:<EXTERNAL_PORT>/swagger`. Do **not** test from your home Wi-Fi — most
  home routers can't loop back to their own public IP (NAT hairpin), so a home-network test
  can fail even when external access works fine. An online "open port check" tool against
  `82.76.59.60:<EXTERNAL_PORT>` is a quick alternative.
- **Check your public IP hasn't changed** since you told testers:
  ```powershell
  (Invoke-RestMethod https://api.ipify.org)
  ```

### Step 5. Test
Players launch their desktop clients (with `IHL_API_BASE` set) and you run through the
functionality you want to exercise / capture test cases for.

### Step 6. Shut down when finished
`Ctrl+C` in the AppHost terminal — this stops the API, logs the Steam bots off, and stops the
frontend/Discord. The SQL container is **persistent by design**, so your data (registrations,
matches, ratings) survives to the next session.

The firewall rule and the portproxy bridge are harmless to leave in place for next time — no
need to remove them between sessions.

---

## Teardown (only if you want to remove the exposure)

```powershell
# Remove the port bridge
netsh interface portproxy delete v4tov4 listenaddress=0.0.0.0 listenport=5193
# Remove the firewall rule
Get-NetFirewallRule -DisplayName "IHL API 5193" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
```
(And, if you no longer want to host at all, remove the port-forward on your router.)

---

## Security & reliability notes (read once)

- **Plain HTTP over the internet.** There's no TLS, so player JWTs and the Steam return token
  travel in cleartext. Acceptable for a private test among friends; do **not** treat this as
  production. For encryption later, put a lightweight reverse proxy with an automatic cert
  (e.g. Caddy) in front of `5193` and forward `443` on the router instead.
- **Admin key.** Keep `GameManager:AdminApiKey` set whenever the server is publicly reachable
  (Part 1, Step 3) — otherwise admin endpoints are open to anyone who finds the URL.
- **Dynamic public IP.** `82.76.59.60` can change. For a stable address testers set once, use
  a free DDNS provider (DuckDNS, No-IP): install its updater on this PC and hand testers
  `http://yourname.duckdns.org:<EXTERNAL_PORT>` instead of the raw IP.
- **Bot credentials.** `IHLDota2Bot/appsettings.json` contains Steam bot passwords in
  plaintext. That predates this setup, but since the API is now internet-reachable, consider
  moving them to user-secrets / environment variables.

---

## Why local hosting (background)

The API (`IHLDota2Bot`) is stateful/always-on: `SteamGcClientPool` is an `IHostedService`
holding live SteamKit connections. Free cloud tiers that sleep or scale to zero drop those
connections, and no Azure tier is both permanently free and always-on. Since the server only
runs during your ~1-hour test sessions, running it locally is free and fits the workflow. The
AppHost change pins the backend to a fixed port (5193) so the firewall/bridge rules stay valid
across runs.

---

## Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| `localhost:5193/swagger` works, public URL doesn't | Firewall rule or portproxy bridge missing — re-run Part 1 Step 2 and re-check `netstat` (Part 2 Step 3). |
| `netstat` shows only `127.0.0.1:5193`, no `0.0.0.0:5193` | The portproxy bridge isn't registered — re-run Part 1 Step 2 in an **elevated** shell. |
| `netsh portproxy add` seems to do nothing / errors | Run it elevated; ensure the `iphlpsvc` (IP Helper) service is Running. |
| Public URL works from home Wi-Fi but not cellular | You were testing via NAT loopback. Always verify external reachability from a network outside your home. |
| Nothing reachable externally at all | Router forward wrong (external port not mapped to `192.168.1.10:5193`), or ISP uses CGNAT — confirm `82.76.59.60` matches what an external "what is my IP" site reports. |
| Steam login fails for players | Confirm `IHL_API_BASE` is the exact public `http://IP:<EXTERNAL_PORT>`, that they opened a fresh terminal after `setx`, and that the same port is forwarded and open. |
| Player address stopped working next day | Public IP changed — set up DDNS, or hand out the new IP. |
| Admin-key warning in the API logs | Set `GameManager:AdminApiKey` (Part 1 Step 3). |
| Port bridge conflict / `0.0.0.0:5193` already in use on startup | An old server process (or a reintroduced all-interfaces bind) still holds the port — close it, or don't bind Kestrel to `0.0.0.0` directly; the bridge owns that address. |
