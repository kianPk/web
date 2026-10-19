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

## Runtime Process Integrity Monitor (v0.5+)

User-mode module that attaches to the live CS2 process after launch:

| Check | What it does |
|--------|----------------|
| Game identity | Verifies `cs2.exe` under Steam tree + records Authenticode |
| Loaded modules | Baselines modules; flags new unsigned / hash-changed modules |
| Memory protection | `VirtualQueryEx` for private executable regions outside modules |
| Process access | `CheckRemoteDebuggerPresent` + `ProcessDebugPort` |
| Overlay heuristic | Multi-signal (layered/transparent/size/title) — not TOPMOST alone |
| Process gate | Blocks CS2 while unauthorized unsigned apps are open; lifts on game exit |

**Default: Audit-only** (`RuntimeProcessIntegrityMonitor.AuditOnly = true`).

- Writes `\%LocalAppData%\YGuardAC\integrity-audit.log`
- Does **not** ban or kill on integrity alone until AuditOnly is flipped to `false`
- Confirmed tamper requires **multi-signal** criteria (never a single weak event)

### User-mode limitations

- No visibility into kernel / hypervisor cheats
- Handle / memory queries can fail without sufficient rights
- Signed loaders and mapped images can evade heuristics
- Overlay detection is best-effort — never ban on overlay alone

Flip enforcement only after reviewing audit logs:

```csharp
RuntimeProcessIntegrityMonitor.AuditOnly = false;
```

## Honesty

This is a **client attestation gate + user-mode integrity monitor**, not a kernel anti-cheat. It raises the bar and matches FACEIT-style UX; bypass is still possible. Keep server-side CSS anti-cheat for in-match detection.
