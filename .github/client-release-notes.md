## Dota 2 In-House League - Desktop Client

Windows 64-bit. Self-contained - no .NET install required.

### Install & run
1. Download **IHLClient-win-x64.zip** below.
2. Unzip it to a folder (keep all files together - don't move the .exe out on its own).
3. Point it at the league server (one-time), then launch:
   - In PowerShell: `setx IHL_API_BASE "<server URL your host gave you>"`
   - Open a **new** terminal (so the setting applies), then run `IHLDota2Bot.Desktop.exe`.
4. Log in with Steam when prompted.

### Notes
- Windows SmartScreen may warn "unknown publisher" (the app isn't code-signed). Click **More info -> Run anyway**.
- Keep the `.dll` files next to `IHLDota2Bot.Desktop.exe`.
