# YGuard Anti-Cheat Launcher (MVP)

Windows tray-style WinForms app that mirrors FACEIT AC's **hardware gate**:

- Secure Boot
- IOMMU / VBS (best-effort)
- TPM 2.0
- TPM Attestation (MVP = TPM present)
- HVCI
- Windows Security Updates (soft)

## Flow

1. Player opens https://yguard.ir/ac while logged in → **Generate pair code**
2. Launcher → enter code → **Pair device**
3. **Run checks** → **Submit & unlock queue**
4. API stores attestation (TTL default 15 min)
5. When `public.ac_launcher_required=true`, `matchmaking:join-queue` rejects lobbies without a fresh pass

## Build

```powershell
cd yguard-ac-launcher
dotnet build -c Release
# exe: bin\Release\net8.0-windows\YGuardAC.exe
```

## API

| Method | Path | Auth |
|--------|------|------|
| GET | `/plugins/ac/requirements` | public |
| POST | `/plugins/ac/pair/start` | session cookie |
| POST | `/plugins/ac/pair/claim` | public (code) |
| POST | `/plugins/ac/attest` | `Bearer <device_token>` |
| GET | `/plugins/ac/status` | session cookie |

> Paths are under `/plugins/ac` because Arvan CDN only forwards known API prefixes; bare `/ac/*` returns nginx 404.

## Admin

```sql
UPDATE settings SET value = 'true' WHERE name = 'public.ac_launcher_required';
```

Keep it `false` until players have the launcher.

## Honesty

This is a **client attestation gate**, not a kernel anti-cheat. It raises the bar and matches FACEIT-style UX; bypass is still possible. Keep TB Anti-Cheat (server CSS) for in-match detection.
