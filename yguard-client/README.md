# YGuard Client

Windows desktop app (WebView2) that opens the YGuard Faceit-style shell so players can Play / Servers / Club / … without living in the browser.

## Requirements

- Windows 10/11 x64
- Steam running and logged in
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (usually already installed with Edge)

## Build

```powershell
cd yguard-client
dotnet publish -c Release -r win-x64 --self-contained true
# exe: bin\Release\net8.0-windows\win-x64\publish\YGuardClient.exe
```

Optional env:

| Variable | Meaning |
|----------|---------|
| `YGUARD_CLIENT_URL` | Override start URL (default `https://yguard.ir/launcher?desktop=1`) |
| `YGUARD_WEB_BASE` | API/web origin for ticket login (default `https://yguard.ir`) |
| `STEAM_AUTH_APP_ID` | Steamworks app id (default `480` Spacewar; must match API) |
| `YGUARD_CLIENT_DEVTOOLS` | `1` to enable DevTools |

## Behaviour

- On start: reads the local Steam account, sends an auth ticket to `POST /auth/steam/ticket`, then opens `/auth/steam/desktop?token=…` so the panel session is created (or the player is registered) without a Steam browser login
- Fallback: if Steam is closed / ticket fails, loads `/launcher` and the normal site login still works
- Loads `/launcher` inside an edge-less window
- `steam://`, `ts3server://`, Discord links open via the OS
- Session cookies stay in `%LocalAppData%\YGuardClient\WebView2`
- Web UI can send `minimize` / `maximize` / `close` / `drag-start` via `chrome.webview.postMessage`
