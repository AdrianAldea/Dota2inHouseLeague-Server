# Hosting the API for real-world testing (Cloudflare Tunnel)

How to expose the Game Manager API from your PC so other players can connect over the
internet during a test session. Free, and it works from behind CGNAT.

Fits an "open → test for ~1 hour → close" workflow: the API holds persistent Steam bot
connections while it runs, so it only needs to be up during the session.

---

## Why a tunnel (not router port-forwarding)

This connection is behind **CGNAT** (carrier-grade NAT). The router's WAN IP is a
`100.64.0.0/10` address (e.g. `100.67.230.205`), not the public IP that `api.ipify.org`
reports (`82.76.59.60`) — that public IP is shared across many ISP customers. Inbound
connections never reach the router, so **port-forwarding cannot work on this line**, no
matter how the router rule is configured.

A tunnel sidesteps this: `cloudflared` makes an **outbound** connection from the PC to
Cloudflare's edge, and players reach a public Cloudflare URL that rides back down that
tunnel. No inbound port, no public IP required. (Because of this, the old Windows firewall
rule and `netsh portproxy` bridge from the port-forwarding attempt are **not needed** — see
"Optional cleanup".)

---

## Your environment (as of 2026-07-13)

| Thing | Value | Notes |
|---|---|---|
| API internal target | `http://localhost:5193` | Fixed by the AppHost change; cloudflared points here. |
| cloudflared | `C:\Program Files (x86)\cloudflared\cloudflared.exe` | Installed via `winget install Cloudflare.cloudflared`. |
| Tunnel type | Quick tunnel (`--url`) | Free, no account. **URL is random and changes every cloudflared restart.** |

---

## Prerequisites

- **Docker Desktop** — the AppHost provisions the SQL Server container.
- **.NET SDK** — to run the AppHost.
- **cloudflared** — install once:
  ```powershell
  winget install --id Cloudflare.cloudflared -e --accept-package-agreements --accept-source-agreements
  ```

---

## One-time setup

### Set the admin API key
Admin endpoints are unrestricted until this is set (the API logs a warning at startup). Set
it before exposing the server publicly — no elevation needed:
```powershell
dotnet user-secrets --project IHLDota2Bot set "GameManager:AdminApiKey" "<choose-a-secret>"
```

That's the only one-time step. Unlike port-forwarding, the tunnel needs no firewall or
router changes.

### Optional: hide Swagger during public sessions
Swagger UI is enabled in Development and therefore reachable through the tunnel. It only
*documents* the API (auth still applies), but if you'd rather not advertise the surface,
disable it for the session before starting the AppHost:
```powershell
$env:Swagger__Enabled = "false"
dotnet run --project IHLDota2Bot.AppHost
```
With Swagger off, verify the tunnel with `"$u/health"` instead of the swagger URL in
Step 4 (expect 200).

---

## Every test session

### Step 1. Start Docker Desktop
So the AppHost can start the SQL Server container.

### Step 2. Start the full stack
```powershell
dotnet run --project IHLDota2Bot.AppHost
```
Brings up SQL Server (container, port 52001), the Game Manager API on **5193**, and the
Steam bot accounts (they log in on boot). Wait in the Aspire dashboard until
`Dota2inHouseLeague-Backend` is **Running / Healthy**.

> The Discord bot and Angular frontend also start by default; they aren't needed for API
> testing. Ignore them, or Stop those two resources from the dashboard.

### Step 3. Start the tunnel (second terminal)
```powershell
& "C:\Program Files (x86)\cloudflared\cloudflared.exe" tunnel --url http://localhost:5193
```
Within a few seconds it prints a line like:
```
+--------------------------------------------------------------------------------------------+
|  Your quick Tunnel has been created! Visit it at:                                          |
|  https://<random-words>.trycloudflare.com                                                  |
+--------------------------------------------------------------------------------------------+
```
Copy that `https://<random-words>.trycloudflare.com` URL — that's what players use. **Leave
this terminal running** for the whole session; closing it drops the tunnel.

### Step 4. Verify (from this PC is fine — the request still goes out to Cloudflare and back)
```powershell
$u = "https://<random-words>.trycloudflare.com"   # paste your URL
(Invoke-WebRequest "$u/swagger/index.html" -TimeoutSec 20 -UseBasicParsing).StatusCode  # expect 200
```
A `200` means the public path (Cloudflare edge → tunnel → Kestrel) is good.

### Step 5. Testers connect
Give each tester the URL. On their machine, once per URL:
```powershell
setx IHL_API_BASE "https://<random-words>.trycloudflare.com"
```
> `setx` affects **future** processes — they must open a **new** terminal (or restart the
> desktop client) for it to take effect. Then launch the desktop client. Steam login works:
> the API emits `https://` realm/return_to (see "How Steam login stays HTTPS"), and the
> client handles the final callback on the player's own loopback.

### Step 6. Shut down
- `Ctrl+C` the cloudflared terminal (drops the public URL).
- `Ctrl+C` the AppHost terminal (stops the API, logs the Steam bots off). The SQL container
  is persistent, so data survives to the next session.

---

## How Steam login stays HTTPS (background)

`cloudflared` terminates TLS at Cloudflare's edge and forwards plain HTTP to Kestrel, so
`ctx.Request.Scheme` would be `http` and the Steam OpenID `realm`/`return_to` would come out
as `http://` even though players are on `https`. `IHLDota2Bot/Program.cs` calls
`UseForwardedHeaders` honouring `X-Forwarded-Proto`, **trusted only from the loopback
cloudflared process** (`KnownProxies` = `127.0.0.1` + `::1`), so the scheme reflects the
public `https` URL without letting a remote caller spoof it. Verified: the login redirect
emits `openid.realm=https://<random-words>.trycloudflare.com`.

---

## A permanent URL (optional, later)

Quick tunnels hand out a new random URL every time cloudflared starts, so testers re-set
`IHL_API_BASE` each session. For a **stable** URL you need a **named tunnel**, which requires
a domain added to your Cloudflare account:
1. Add a domain to Cloudflare (you must own one — Cloudflare doesn't give free domains).
2. `cloudflared tunnel login`, `cloudflared tunnel create ihl`, map a hostname via
   `cloudflared tunnel route dns ihl ihl.yourdomain.com`, and run with a config file.
Then the URL (`https://ihl.yourdomain.com`) never changes. Ask and I'll set this up.

---

## Optional cleanup (leftover port-forwarding artifacts)

The earlier port-forwarding attempt left a firewall rule and a portproxy bridge that are now
unused. They're harmless, but to remove them (elevated PowerShell):
```powershell
netsh interface portproxy delete v4tov6 listenaddress=0.0.0.0 listenport=5193
Get-NetFirewallRule -DisplayName "IHL API 5193" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
```
You can also delete/disable the `DotaBot` port-forward rule on the router — it can't work
under CGNAT anyway.

---

## Why local hosting (background)

The API (`IHLDota2Bot`) is stateful/always-on: `SteamGcClientPool` is an `IHostedService`
holding live SteamKit connections. Free cloud tiers that sleep or scale to zero drop those
connections, and no Azure tier is both permanently free and always-on. Since the server only
runs during your ~1-hour test sessions, running it locally + tunnelling is free and fits the
workflow. The AppHost change pins the backend to a fixed port (5193) so cloudflared's target
is stable across runs.

---

## Troubleshooting

| Symptom | Likely cause / fix |
|---|---|
| cloudflared prints a URL but it 502s | The backend isn't up yet — wait for `Dota2inHouseLeague-Backend` = Healthy, then retry. |
| Tunnel URL changed since last session | Expected for quick tunnels — it's random per start. Re-share the URL, or set up a named tunnel. |
| Steam login fails for players | Confirm `IHL_API_BASE` is the exact `https://…trycloudflare.com`, and that they opened a fresh terminal after `setx`. The API must be on the build with `UseForwardedHeaders`. |
| `swagger` 200 locally but not via the URL | cloudflared terminal was closed or lost connection — restart it (this yields a new URL). |
| Admin-key warning in the API logs | Set `GameManager:AdminApiKey` (One-time setup). |
| Players on the desktop client can't authenticate but swagger loads | Ensure they set `IHL_API_BASE` (env var) — the bundled `appsettings.json` defaults to localhost. |
