using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace YGuardAC;

/// <summary>
/// Continuously finds processes whose main image is not trusted Authenticode
/// and not under a critical OS/Steam/GPU path, then TerminateProcess them.
///
/// Local soft enforcement only — never reports to the API / never bans.
/// Scans ALL processes on a timer (not only newly started), so cheats that
/// were already open before AC started are still closed.
/// </summary>
internal sealed class UnsignedProcessGuard : IDisposable
{
    private ManagementEventWatcher? _watcher;
    private System.Threading.Timer? _sweepTimer;
    private readonly HashSet<int> _killedOrSafe = new();
    private readonly Dictionary<string, bool> _sigCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private bool _started;
    private int _terminated;

    public int TerminatedCount => _terminated;

    public void Start()
    {
        if (_started) return;
        _started = true;

        // Immediate full sweep — catches cheats already running (DH, etc.).
        ThreadPool.QueueUserWorkItem(_ => SweepAll());

        _sweepTimer = new System.Threading.Timer(
            _ => SweepAll(),
            null,
            TimeSpan.FromSeconds(1.5),
            TimeSpan.FromSeconds(1.5));

        try
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
            _watcher = new ManagementEventWatcher(query);
            _watcher.EventArrived += OnProcessStarted;
            _watcher.Start();
        }
        catch
        {
            try { _watcher?.Dispose(); } catch { }
            _watcher = null;
        }
    }

    public void Dispose()
    {
        try
        {
            if (_watcher != null)
            {
                _watcher.EventArrived -= OnProcessStarted;
                try { _watcher.Stop(); } catch { }
                _watcher.Dispose();
                _watcher = null;
            }
        }
        catch { }

        try { _sweepTimer?.Dispose(); } catch { }
        _sweepTimer = null;
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var pidObj = e.NewEvent["ProcessID"];
            var nameObj = e.NewEvent["ProcessName"];
            if (pidObj is null) return;
            var pid = Convert.ToInt32(pidObj);
            var name = nameObj?.ToString() ?? "";
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Thread.Sleep(120);
                    EvaluateProcess(pid, name);
                }
                catch { }
            });
        }
        catch { }
    }

    private void SweepAll()
    {
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    EvaluateProcess(p.Id, p.ProcessName ?? "");
                }
                catch { }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }
        }
        catch { }
    }

    private void EvaluateProcess(int pid, string processName)
    {
        if (pid <= 4) return;
        if (pid == Environment.ProcessId) return;

        lock (_gate)
        {
            if (_killedOrSafe.Contains(pid))
                return;
        }

        var path = TryGetProcessPath(pid);
        if (string.IsNullOrWhiteSpace(processName))
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                processName = p.ProcessName ?? "";
            }
            catch
            {
                return;
            }
        }

        // No path yet — try again on next sweep (do NOT mark safe).
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        if (IsCriticalAllowlisted(path, processName))
        {
            MarkSafe(pid);
            return;
        }

        if (HasTrustedSignatureCached(path))
        {
            MarkSafe(pid);
            return;
        }

        // Unsigned / invalid signature + not critical → close only (no ban).
        if (TryTerminate(pid))
        {
            Interlocked.Increment(ref _terminated);
            MarkSafe(pid); // don't thrash if PID reused slowly
        }
    }

    private void MarkSafe(int pid)
    {
        lock (_gate)
        {
            _killedOrSafe.Add(pid);
            if (_killedOrSafe.Count > 8000)
                _killedOrSafe.Clear();
        }
    }

    private bool HasTrustedSignatureCached(string path)
    {
        lock (_gate)
        {
            if (_sigCache.TryGetValue(path, out var cached))
                return cached;
        }

        var ok = false;
        try { ok = CodeSignVerifier.HasValidSignature(path); }
        catch { ok = false; }

        lock (_gate)
        {
            if (_sigCache.Count > 3000)
                _sigCache.Clear();
            _sigCache[path] = ok;
        }

        return ok;
    }

    private static string? TryGetProcessPath(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            try
            {
                var fromModule = p.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(fromModule))
                    return fromModule;
            }
            catch { /* access denied */ }
        }
        catch
        {
            return null;
        }

        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (h == IntPtr.Zero)
            return null;
        try
        {
            var sb = new System.Text.StringBuilder(1024);
            var size = (uint)sb.Capacity;
            if (QueryFullProcessImageName(h, 0, sb, ref size) && size > 0)
                return sb.ToString();
        }
        finally
        {
            CloseHandle(h);
        }
        return null;
    }

    /// <summary>
    /// Narrow allowlist: OS / Steam / GPU only. Do NOT blanket Program Files —
    /// unsigned cheats routinely live there. Signed apps pass via Authenticode.
    /// </summary>
    private static bool IsCriticalAllowlisted(string path, string processName)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var name = (processName ?? "").Trim().ToLowerInvariant();
            var file = Path.GetFileName(full);
            var lower = full.ToLowerInvariant();

            var self = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(self) &&
                full.Equals(Path.GetFullPath(self), StringComparison.OrdinalIgnoreCase))
                return true;

            if (file.Equals("YGuardAC.exe", StringComparison.OrdinalIgnoreCase))
                return true;

            var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrWhiteSpace(win) &&
                full.StartsWith(win.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                return true;

            // Steam + CS2 (Valve shipping mixes; never kill mid-match)
            if (name is "steam" or "steamwebhelper" or "steamservice" or "gameoverlayui"
                or "cs2" or "csgo" or "steamerrorreporter")
                return true;

            if (lower.Contains("\\steam\\") ||
                lower.Contains("\\steamapps\\") ||
                lower.Contains("\\counter-strike"))
                return true;

            // Official GPU stacks
            if (lower.Contains("\\nvidia\\") ||
                lower.Contains("\\amd\\") ||
                lower.Contains("\\ati technologies\\") ||
                lower.Contains("\\intel\\") ||
                name.StartsWith("nv", StringComparison.Ordinal) ||
                name.Contains("nvidia", StringComparison.Ordinal) ||
                name.Contains("radeon", StringComparison.Ordinal) ||
                name.Contains("amddvr", StringComparison.Ordinal))
                return true;
        }
        catch { /* fall through */ }

        return false;
    }

    private static bool TryTerminate(int pid)
    {
        const uint PROCESS_TERMINATE = 0x0001;
        var handle = OpenProcess(PROCESS_TERMINATE, false, (uint)pid);
        if (handle == IntPtr.Zero)
            return false;
        try
        {
            return TerminateProcess(handle, 1);
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(
        uint dwDesiredAccess,
        bool bInheritHandle,
        uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(
        IntPtr hProcess,
        uint dwFlags,
        System.Text.StringBuilder lpExeName,
        ref uint lpdwSize);
}
