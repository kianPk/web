using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace YGuardAC;

/// <summary>
/// Runtime Process Integrity Monitor — user-mode CS2 process integrity.
///
/// Audit-only by default: records detections with evidence + confidence.
/// Enforcement (kill CS2 + server report) only when <see cref="AuditOnly"/> is
/// false AND confirmed multi-signal criteria are met. A single weak signal is
/// never treated as definitive cheating.
///
/// User-mode limitations (documented):
/// - Cannot see kernel cheats / hypervisor-level hooks.
/// - Handle enumeration to foreign processes may be incomplete without SeDebug.
/// - VirtualQueryEx / module snapshots require PROCESS_QUERY / VM_READ access.
/// - Signed malware and DLL maps via legitimate loaders can evade heuristics.
/// - Overlay detection is best-effort; do not ban on overlay alone.
/// </summary>
internal sealed class RuntimeProcessIntegrityMonitor : IDisposable
{
    /// <summary>When true, never kill CS2 or call the ban report for integrity hits.</summary>
    public static bool AuditOnly { get; set; } = true;

    private readonly System.Threading.Timer _timer;
    private readonly object _gate = new();
    private readonly ConcurrentQueue<IntegrityEvent> _events = new();
    private readonly List<IntegrityEvent> _recent = new();
    private readonly Dictionary<string, string> _moduleBaseline = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownModules = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<IntPtr> _knownExecRegions = new();
    private readonly Action<string>? _status;
    private readonly Func<Task>? _onConfirmedTamperReport;
    private bool _disposed;
    private int? _gamePid;
    private string? _gamePath;
    private bool _baselineReady;
    private bool _sessionRestricted;
    private string? _blockReason;
    private DateTime _lastConfirmUtc = DateTime.MinValue;

    public int EventCount { get; private set; }
    public bool SessionRestricted => _sessionRestricted;
    public string? BlockReason => _blockReason;

    public RuntimeProcessIntegrityMonitor(
        Action<string>? status = null,
        Func<Task>? onConfirmedTamperReport = null)
    {
        _status = status;
        _onConfirmedTamperReport = onConfirmedTamperReport;
        _timer = new System.Threading.Timer(
            _ => Tick(),
            null,
            TimeSpan.FromSeconds(1.5),
            TimeSpan.FromSeconds(3));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _timer.Dispose(); } catch { }
    }

    public IReadOnlyList<IntegrityEvent> SnapshotRecent(int take = 40)
    {
        lock (_gate)
        {
            return _recent
                .OrderByDescending(e => e.Utc)
                .Take(take)
                .ToList();
        }
    }

    /// <summary>Called when CS2 is no longer running — lift temporary session gates.</summary>
    public void NotifyGameExited()
    {
        lock (_gate)
        {
            _gamePid = null;
            _gamePath = null;
            _baselineReady = false;
            _moduleBaseline.Clear();
            _knownModules.Clear();
            _knownExecRegions.Clear();
            _sessionRestricted = false;
            _blockReason = null;
        }
    }

    private void Tick()
    {
        if (_disposed) return;
        try
        {
            if (!TryAttachGame(out var pid, out var path))
            {
                if (_gamePid != null)
                    NotifyGameExited();
                return;
            }

            // Policy: unauthorized apps block entering / staying in game.
            if (TryEnforceProcessGate(out var blocker))
            {
                _sessionRestricted = true;
                _blockReason = blocker;
                _status?.Invoke(
                    AuditOnly
                        ? $"Audit | Unauthorized process open: {blocker}"
                        : $"Blocked | Close {blocker} to play CS2");
                if (!AuditOnly)
                    KillGameProcess(pid);
                Emit(new IntegrityEvent(
                    "process_gate",
                    $"Unauthorized process while CS2 active: {blocker}",
                    55,
                    new Dictionary<string, string>
                    {
                        ["blocker"] = blocker,
                        ["audit_only"] = AuditOnly.ToString(),
                    }));
                return;
            }

            _sessionRestricted = false;
            _blockReason = null;

            if (!_baselineReady || _gamePid != pid)
            {
                if (!VerifyGameIdentity(pid, path, out var idEvidence))
                {
                    Emit(new IntegrityEvent(
                        "game_identity",
                        "CS2 process identity could not be verified",
                        80,
                        idEvidence));
                    MaybeConfirm();
                    return;
                }

                BuildModuleBaseline(pid);
                _gamePid = pid;
                _gamePath = path;
                _baselineReady = true;
                Emit(new IntegrityEvent(
                    "game_identity",
                    "CS2 process attached and identity verified",
                    10,
                    new Dictionary<string, string>
                    {
                        ["pid"] = pid.ToString(),
                        ["path"] = path,
                    }));
            }

            ScanLoadedModules(pid);
            ScanExecutableMemory(pid);
            ScanDebugAccess(pid);
            ScanOverlays(pid);
            MaybeConfirm();
        }
        catch
        {
            /* never throw from timer */
        }
    }

    private static bool TryAttachGame(out int pid, out string path)
    {
        pid = 0;
        path = "";
        foreach (var name in new[] { "cs2", "csgo" })
        {
            Process[] procs;
            try { procs = Process.GetProcessesByName(name); }
            catch { continue; }
            foreach (var p in procs)
            {
                try
                {
                    if (p.HasExited) continue;
                    var img = ProcessPolicy.TryGetImagePath(p.Id)
                              ?? TryPathViaToolhelp(p.Id);
                    if (string.IsNullOrWhiteSpace(img))
                        continue;
                    pid = p.Id;
                    path = img;
                    return true;
                }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }
        }
        return false;
    }

    private static bool VerifyGameIdentity(
        int pid,
        string path,
        out Dictionary<string, string> evidence)
    {
        evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["pid"] = pid.ToString(),
            ["path"] = path,
        };

        try
        {
            var file = Path.GetFileName(path);
            if (!file.Equals("cs2.exe", StringComparison.OrdinalIgnoreCase) &&
                !file.Equals("csgo.exe", StringComparison.OrdinalIgnoreCase))
            {
                evidence["reason"] = "unexpected_exe_name";
                return false;
            }

            var lower = path.ToLowerInvariant();
            var underSteam = lower.Contains("\\steamapps\\") ||
                             lower.Contains("\\steam\\") ||
                             lower.Contains("\\counter-strike");
            evidence["under_steam_tree"] = underSteam.ToString();
            if (!underSteam)
            {
                evidence["reason"] = "not_under_steam_tree";
                return false;
            }

            // Valve builds are typically signed; missing signature is suspicious but
            // not alone definitive (some installs / cracked copies) — confidence handled by caller.
            var signed = false;
            try { signed = CodeSignVerifier.HasValidSignature(path); } catch { }
            evidence["authenticode"] = signed.ToString();
            return true;
        }
        catch (Exception ex)
        {
            evidence["error"] = ex.GetType().Name;
            return false;
        }
    }

    private void BuildModuleBaseline(int pid)
    {
        lock (_gate)
        {
            _moduleBaseline.Clear();
            _knownModules.Clear();
            _knownExecRegions.Clear();
        }

        foreach (var (modPath, _) in EnumerateModules(pid))
        {
            var hash = TryFileSha256(modPath);
            lock (_gate)
            {
                _knownModules.Add(modPath);
                if (hash != null)
                    _moduleBaseline[modPath] = hash;
            }
        }
        _baselineReady = true;
    }

    private void ScanLoadedModules(int pid)
    {
        foreach (var (modPath, bas) in EnumerateModules(pid))
        {
            var file = Path.GetFileName(modPath);
            if (file.Equals("cs2.exe", StringComparison.OrdinalIgnoreCase) ||
                file.Equals("csgo.exe", StringComparison.OrdinalIgnoreCase))
                continue;

            var underOk = ProcessPolicy.IsUnderAllowedPath(modPath);
            var signed = false;
            try { signed = CodeSignVerifier.HasValidSignature(modPath); } catch { }

            string? hash;
            bool known;
            string? baselineHash;
            lock (_gate)
            {
                known = _knownModules.Contains(modPath);
                _moduleBaseline.TryGetValue(modPath, out baselineHash);
            }

            hash = TryFileSha256(modPath);

            if (!known)
            {
                lock (_gate)
                {
                    _knownModules.Add(modPath);
                    if (hash != null)
                        _moduleBaseline[modPath] = hash;
                }

                if (!underOk && !signed)
                {
                    Emit(new IntegrityEvent(
                        "loaded_modules",
                        "Unexpected unsigned module in CS2",
                        65,
                        new Dictionary<string, string>
                        {
                            ["module"] = modPath,
                            ["base"] = bas.ToString("X"),
                            ["signed"] = "false",
                        }));
                }
                else if (!underOk)
                {
                    Emit(new IntegrityEvent(
                        "loaded_modules",
                        "New module outside allowlisted paths",
                        35,
                        new Dictionary<string, string>
                        {
                            ["module"] = modPath,
                            ["signed"] = signed.ToString(),
                        }));
                }
                continue;
            }

            if (hash != null && baselineHash != null &&
                !hash.Equals(baselineHash, StringComparison.OrdinalIgnoreCase))
            {
                Emit(new IntegrityEvent(
                    "code_integrity",
                    "On-disk module hash changed after baseline",
                    75,
                    new Dictionary<string, string>
                    {
                        ["module"] = modPath,
                        ["baseline"] = baselineHash,
                        ["current"] = hash,
                    }));
                lock (_gate) { _moduleBaseline[modPath] = hash; }
            }
        }
    }

    private void ScanExecutableMemory(int pid)
    {
        const uint PROCESS_QUERY_INFORMATION = 0x0400;
        const uint PROCESS_VM_READ = 0x0010;
        var h = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, (uint)pid);
        if (h == IntPtr.Zero)
            h = OpenProcess(0x1000 | PROCESS_VM_READ, false, (uint)pid); // LIMITED
        if (h == IntPtr.Zero)
            return;

        try
        {
            var moduleRanges = EnumerateModules(pid)
                .Select(m => (m.Base, Size: EstimateModuleSize(pid, m.Base)))
                .ToList();

            var addr = IntPtr.Zero;
            var anomalous = 0;
            long anomalousBytes = 0;

            for (var i = 0; i < 10_000; i++)
            {
                var mbi = new MEMORY_BASIC_INFORMATION();
                var result = VirtualQueryEx(h, addr, out mbi, (UIntPtr)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
                if (result == UIntPtr.Zero)
                    break;

                var size = mbi.RegionSize.ToInt64();
                if (size <= 0)
                    break;

                var isPrivate = mbi.Type == MEM_PRIVATE;
                var prot = mbi.Protect;
                var exec = (prot & (PAGE_EXECUTE | PAGE_EXECUTE_READ | PAGE_EXECUTE_READWRITE | PAGE_EXECUTE_WRITECOPY)) != 0;
                var committed = mbi.State == MEM_COMMIT;

                if (committed && isPrivate && exec && size >= 64 * 1024)
                {
                    var insideModule = moduleRanges.Any(r =>
                        addrLong(mbi.BaseAddress) >= addrLong(r.Base) &&
                        addrLong(mbi.BaseAddress) < addrLong(r.Base) + Math.Max(r.Size, 0x1000));

                    if (!insideModule)
                    {
                        anomalous++;
                        anomalousBytes += size;
                        var key = mbi.BaseAddress;
                        var firstSeen = false;
                        lock (_gate)
                        {
                            firstSeen = _knownExecRegions.Add(key);
                        }
                        if (firstSeen)
                        {
                            Emit(new IntegrityEvent(
                                "memory_protection",
                                "Private executable memory outside known modules",
                                size >= 256 * 1024 ? 78 : 62,
                                new Dictionary<string, string>
                                {
                                    ["base"] = mbi.BaseAddress.ToString("X"),
                                    ["size"] = size.ToString(),
                                    ["protect"] = prot.ToString("X"),
                                }));
                        }
                    }
                }

                var next = addrLong(mbi.BaseAddress) + size;
                if (next <= addrLong(addr))
                    break;
                addr = new IntPtr(next);
            }

            if (anomalous >= 3 && anomalousBytes >= 512 * 1024)
            {
                Emit(new IntegrityEvent(
                    "memory_protection",
                    "Multiple anomalous private RX/RWX regions",
                    82,
                    new Dictionary<string, string>
                    {
                        ["regions"] = anomalous.ToString(),
                        ["bytes"] = anomalousBytes.ToString(),
                    }));
            }
        }
        finally
        {
            CloseHandle(h);
        }
    }

    private void ScanDebugAccess(int pid)
    {
        const uint PROCESS_QUERY_INFORMATION = 0x0400;
        var h = OpenProcess(PROCESS_QUERY_INFORMATION, false, (uint)pid);
        if (h == IntPtr.Zero)
            return;
        try
        {
            if (CheckRemoteDebuggerPresent(h, out var debugged) && debugged)
            {
                Emit(new IntegrityEvent(
                    "process_access",
                    "Remote debugger present on CS2 process",
                    92,
                    new Dictionary<string, string>
                    {
                        ["pid"] = pid.ToString(),
                        ["api"] = "CheckRemoteDebuggerPresent",
                    }));
            }

            // ProcessDebugPort (7) — non-zero often means a debugger.
            var status = NtQueryInformationProcess(
                h,
                7,
                out var debugPort,
                IntPtr.Size,
                out _);
            if (status == 0 && debugPort != IntPtr.Zero)
            {
                Emit(new IntegrityEvent(
                    "process_access",
                    "ProcessDebugPort set on CS2",
                    90,
                    new Dictionary<string, string>
                    {
                        ["pid"] = pid.ToString(),
                        ["debug_port"] = debugPort.ToString("X"),
                    }));
            }
        }
        finally
        {
            CloseHandle(h);
        }
    }

    /// <summary>
    /// Overlay heuristic: does NOT rely on WS_EX_TOPMOST alone.
    /// Requires a combination of layered / transparent / size-match / empty title.
    /// </summary>
    private void ScanOverlays(int gamePid)
    {
        if (!TryGetGameBounds(gamePid, out var gameRect))
            return;

        var gameW = gameRect.Right - gameRect.Left;
        var gameH = gameRect.Bottom - gameRect.Top;
        if (gameW < 800 || gameH < 600)
            return;

        EnumWindows((hWnd, _) =>
        {
            try
            {
                if (!IsWindowVisible(hWnd)) return true;
                if (!GetWindowRect(hWnd, out var rect)) return true;
                var w = rect.Right - rect.Left;
                var h = rect.Bottom - rect.Top;
                if (Math.Abs(w - gameW) > 6 || Math.Abs(h - gameH) > 6) return true;
                if (Math.Abs(rect.Left - gameRect.Left) > 6 ||
                    Math.Abs(rect.Top - gameRect.Top) > 6) return true;

                GetWindowThreadProcessId(hWnd, out var pid);
                if (pid == 0 || pid == (uint)gamePid) return true;

                string? procName = null;
                string? path = null;
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    procName = p.ProcessName;
                    path = ProcessPolicy.TryGetImagePath(p.Id);
                }
                catch { return true; }

                if (ProcessPolicy.IsAllowed(procName, path))
                    return true;

                var ex = GetWindowLong(hWnd, GWL_EXSTYLE);
                var layered = (ex & WS_EX_LAYERED) != 0;
                var transparent = (ex & WS_EX_TRANSPARENT) != 0;
                var topmost = (ex & WS_EX_TOPMOST) != 0;
                var toolwindow = (ex & WS_EX_TOOLWINDOW) != 0;
                var title = GetWindowTitle(hWnd);
                var blankTitle = string.IsNullOrEmpty(title);

                // Multi-signal score — TOPMOST alone is never enough.
                var score = 0;
                if (layered) score += 2;
                if (transparent) score += 3;
                if (topmost) score += 1;
                if (blankTitle) score += 2;
                if (toolwindow) score += 1;
                if (score < 6) return true;

                Emit(new IntegrityEvent(
                    "runtime_behavior",
                    "Suspicious floating overlay over CS2",
                    Math.Min(70, 30 + score * 5),
                    new Dictionary<string, string>
                    {
                        ["process"] = procName ?? "",
                        ["path"] = path ?? "",
                        ["layered"] = layered.ToString(),
                        ["transparent"] = transparent.ToString(),
                        ["topmost"] = topmost.ToString(),
                        ["blank_title"] = blankTitle.ToString(),
                        ["score"] = score.ToString(),
                    }));

                // Soft close only in enforce mode; audit never kills overlays here
                // (OverlayGuard still handles classic click-through overlays).
                if (!AuditOnly && score >= 7 && blankTitle && layered && transparent)
                {
                    try { PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero); } catch { }
                }
            }
            catch { }
            return true;
        }, IntPtr.Zero);
    }

    private bool TryEnforceProcessGate(out string blockerName)
    {
        blockerName = "";
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id <= 4 || p.Id == Environment.ProcessId) continue;
                    var name = p.ProcessName ?? "";
                    if (ProcessPolicy.IsGameProcessName(name)) continue;
                    var path = ProcessPolicy.TryGetImagePath(p.Id);
                    if (!ProcessPolicy.IsSessionBlocker(name, path))
                        continue;
                    blockerName = string.IsNullOrWhiteSpace(path)
                        ? name
                        : Path.GetFileName(path);
                    return true;
                }
                catch { }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }
        }
        catch { }
        return false;
    }

    private void MaybeConfirm()
    {
        List<IntegrityEvent> window;
        lock (_gate)
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-60);
            window = _recent.Where(e => e.Utc >= cutoff && e.Confidence >= 50).ToList();
        }

        if (window.Count == 0) return;

        var score = window.Sum(e => e.Confidence);
        var hasCritical = window.Any(e => e.Confidence >= 90);
        var distinctTypes = window.Select(e => e.Kind).Distinct(StringComparer.OrdinalIgnoreCase).Count();

        // Never confirm on a single weak signal. Require either:
        // - one critical (>=90) + another corroborating type, or
        // - aggregate confidence >= 140 across 2+ kinds.
        var confirmed =
            (hasCritical && distinctTypes >= 2) ||
            (score >= 140 && distinctTypes >= 2);

        if (!confirmed) return;
        if ((DateTime.UtcNow - _lastConfirmUtc).TotalSeconds < 45) return;
        _lastConfirmUtc = DateTime.UtcNow;

        Emit(new IntegrityEvent(
            "confirmed_tamper",
            "Multi-signal runtime integrity confirmation",
            95,
            new Dictionary<string, string>
            {
                ["aggregate"] = score.ToString(),
                ["kinds"] = string.Join(",", window.Select(e => e.Kind).Distinct()),
                ["audit_only"] = AuditOnly.ToString(),
            }));

        _status?.Invoke(
            AuditOnly
                ? "Audit | Runtime integrity alerts (no enforcement)"
                : "Integrity | Tamper confirmed — stopping game session");

        if (AuditOnly)
            return;

        if (_gamePid is int pid)
            KillGameProcess(pid);

        try
        {
            var report = _onConfirmedTamperReport;
            if (report != null)
                _ = report();
        }
        catch { }
    }

    private void Emit(IntegrityEvent ev)
    {
        EventCount++;
        _events.Enqueue(ev);
        while (_events.Count > 200 && _events.TryDequeue(out _)) { }

        lock (_gate)
        {
            _recent.Add(ev);
            if (_recent.Count > 120)
                _recent.RemoveRange(0, _recent.Count - 120);
        }

        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YGuardAC");
            Directory.CreateDirectory(dir);
            var line =
                $"{ev.Utc:o}\t{ev.Kind}\t{ev.Confidence}\t{ev.Message}\t{FormatEvidence(ev.Evidence)}\r\n";
            File.AppendAllText(Path.Combine(dir, "integrity-audit.log"), line, Encoding.UTF8);
        }
        catch { }
    }

    private static string FormatEvidence(IReadOnlyDictionary<string, string> evidence)
    {
        if (evidence.Count == 0) return "";
        return string.Join(";", evidence.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    private static void KillGameProcess(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            try { p.Kill(entireProcessTree: true); }
            catch { try { p.Kill(); } catch { } }
        }
        catch { }
    }

    private static IEnumerable<(string Path, IntPtr Base)> EnumerateModules(int pid)
    {
        var snap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, (uint)pid);
        if (snap == INVALID_HANDLE_VALUE || snap == IntPtr.Zero)
            yield break;
        try
        {
            var me = new MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>() };
            if (!Module32First(snap, ref me))
                yield break;
            do
            {
                var path = (me.szExePath ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(path))
                    yield return (path, me.modBaseAddr);
            }
            while (Module32Next(snap, ref me));
        }
        finally
        {
            CloseHandle(snap);
        }
    }

    private static long EstimateModuleSize(int pid, IntPtr bas)
    {
        foreach (var (path, b) in EnumerateModules(pid))
        {
            if (b != bas) continue;
            try
            {
                if (File.Exists(path))
                    return new FileInfo(path).Length;
            }
            catch { }
        }
        return 0x100000;
    }

    private static string? TryFileSha256(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var fs = File.OpenRead(path);
            var hash = SHA256.HashData(fs);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryPathViaToolhelp(int pid)
    {
        foreach (var (path, _) in EnumerateModules(pid))
            return path;
        return null;
    }

    private static bool TryGetGameBounds(int gamePid, out RECT rect)
    {
        RECT found = default;
        var ok = false;
        EnumWindows((hWnd, _) =>
        {
            GetWindowThreadProcessId(hWnd, out var pid);
            if (pid != (uint)gamePid) return true;
            if (!IsWindowVisible(hWnd)) return true;
            if (!GetWindowRect(hWnd, out var r)) return true;
            var w = r.Right - r.Left;
            var h = r.Bottom - r.Top;
            if (w < 800 || h < 600) return true;
            found = r;
            ok = true;
            return false;
        }, IntPtr.Zero);
        rect = found;
        return ok;
    }

    private static long addrLong(IntPtr p) =>
        IntPtr.Size == 8 ? p.ToInt64() : (uint)p.ToInt32();

    private static string GetWindowTitle(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        _ = GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString().Trim();
    }

    // --- Win32 ---
    private const uint TH32CS_SNAPMODULE = 0x00000008;
    private const uint TH32CS_SNAPMODULE32 = 0x00000010;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WM_CLOSE = 0x0010;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_PRIVATE = 0x20000;
    private const uint PAGE_EXECUTE = 0x10;
    private const uint PAGE_EXECUTE_READ = 0x20;
    private const uint PAGE_EXECUTE_READWRITE = 0x40;
    private const uint PAGE_EXECUTE_WRITECOPY = 0x80;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern UIntPtr VirtualQueryEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        out MEMORY_BASIC_INFORMATION lpBuffer,
        UIntPtr dwLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, out bool pbDebuggerPresent);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        out IntPtr processInformation,
        int processInformationLength,
        out int returnLength);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MODULEENTRY32
    {
        public uint dwSize;
        public uint th32ModuleID;
        public uint th32ProcessID;
        public uint GlblcntUsage;
        public uint ProccntUsage;
        public IntPtr modBaseAddr;
        public uint modBaseSize;
        public IntPtr hModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExePath;
    }
}

internal sealed record IntegrityEvent(
    string Kind,
    string Message,
    int Confidence,
    IReadOnlyDictionary<string, string> Evidence)
{
    public DateTime Utc { get; } = DateTime.UtcNow;
}
