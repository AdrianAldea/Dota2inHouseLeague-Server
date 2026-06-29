# Publishing the Dota IHL desktop client

The client is a standard Avalonia desktop app. For distribution to players, publish a
**self-contained, single-file** executable so they don't need the .NET runtime installed.

## Recommended: self-contained single-file (Windows)

```bash
dotnet publish IHLDota2Bot.Desktop/IHLDota2Bot.Desktop.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true
```

Output: `IHLDota2Bot.Desktop/bin/Release/net10.0/win-x64/publish/`
- `IHLDota2Bot.Desktop.exe` — the app (branded icon embedded via `<ApplicationIcon>`).
- `appsettings.json` — copied next to the exe so players (or you) can point it at the
  server by editing `ApiBaseUrl`. The `IHL_API_BASE` environment variable overrides it.

**Ship only `IHLDota2Bot.Desktop.exe` + `appsettings.json`.** The publish folder also
contains `.pdb` debug symbols (incl. large native `libSkiaSharp.pdb` / `libHarfBuzzSharp.pdb`) —
these are not needed at runtime and can be deleted. Add `-p:DebugType=none -p:DebugSymbols=false`
to drop the managed `.pdb`; delete the native ones from the output if you want a lean folder.

## Important: do NOT enable trimming

`ViewLocator` resolves views from view-models via **reflection** (it's annotated
`[RequiresUnreferencedCode]`). `PublishTrimmed=true` would strip the view types and the
app would show "Not Found: ...View" at runtime. Leave trimming off (the default here).

## Optional flags

- **Faster startup:** add `-p:PublishReadyToRun=true` (larger file, quicker cold start).
- **Other OSes:** swap the RID — `-r osx-arm64`, `-r osx-x64`, or `-r linux-x64`
  (Avalonia is cross-platform; the DPAPI token encryption falls back to a plain file off Windows — see `TokenStore`).
- **Smaller, runtime-dependent build** (requires .NET 10 on the player's machine):
  drop `--self-contained true` and the single-file flags.

## Configuration recap

| Setting | Where | Default |
|---|---|---|
| API base URL | `appsettings.json` → `ApiBaseUrl`, or `IHL_API_BASE` env var | `http://localhost:5193` |

For production the server should be reachable over **HTTPS**, and the backend must have a
stable `PlayerJwt:SigningKey` configured (see the server's user-secrets / environment).
