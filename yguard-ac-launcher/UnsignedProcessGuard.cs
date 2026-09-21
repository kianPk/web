using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace YGuardAC;

/// <summary>
/// Watches for newly created processes (WMI Win32_ProcessStartTrace).
/// Unsigned images are terminated via PROCESS_TERMINATE + TerminateProcess.
/// This is local soft enforcement only — it never reports to the API and
/// must never cause a platform ban. Existing processes at start are left alone.
/// Critical OS / Steam / Program Files / common launcher paths are allowlisted.
/// </summary>
internal sealed class UnsignedProcessGuard : IDisposable
{
    private ManagementEventWatcher? _watcher;
    private readonly HashSet<int> _recentHandled = new();
    private readonly object _gate = new();
    private bool _started;
    private int _terminated;

    public int TerminatedCount => _terminated;

    public void Start()
    {
        if (_started) return;
        _started = true;

        try
        {
            // Process start events (requires admin). Fallback to polling if WMI fails.
            var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
            _watcher = new ManagementEventWatcher(query);
            _watcher.EventArrived += OnProcessStarted;
            _watcher.Start();
        }
        catch
        {
            try { _watcher?.Dispose(); } catch { }
            _watcher = null;
            StartPollingFallback();
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

        try { _pollTimer?.Dispose(); } catch { }
        _pollTimer = null;
    }

    private System.Threading.Timer? _pollTimer;
    private HashSet<int>? _seenPids;

    private void StartPollingFallback()
    {
        try
        {
            _seenPids = Process.GetProcesses().Select(p =>
            {
                try { return p.Id; }
                finally { try { p.Dispose(); } catch { } }
            }).ToHashSet();
        }
        catch
        {
            _seenPids = new HashSet<int>();
        }

        _pollTimer = new System.Threading.Timer(
            _ => PollOnce(),
            null,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2));
    }

    private void PollOnce()
    {
        if (_seenPids == null) return;
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var id = p.Id;
                    if (!_seenPids.Add(id)) continue;
                    EvaluateNewProcess(id, p.ProcessName);
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

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var pidObj = e.NewEvent["ProcessID"];
            var nameObj = e.NewEvent["ProcessName"];
            if (pidObj is null) return;
            var pid = Convert.ToInt32(pidObj);
            var name = nameObj?.ToString() ?? "";
            EvaluateNewProcess(pid, name);
        }
        catch { }
    }

    private void EvaluateNewProcess(int pid, string processName)
    {
        if (pid <= 4) return; // System / Idle
        if (pid == Environment.ProcessId) return;

        lock (_gate)
        {
            if (!_recentHandled.Add(pid)) return;
            // Cap set size so we don't grow forever.
            if (_recentHandled.Count > 4000)
                _recentHandled.Clear();
        }

        // Give the image path a moment to become queryable.
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                Thread.Sleep(150);
                HandleNewProcess(pid, processName);
            }
            catch { }
        });
    }

    private void HandleNewProcess(int pid, string processName)
    {
        string? path = TryGetProcessPath(pid);
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

        // No path → do not kill blindly (system / protected / short-lived).
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        if (IsAllowlisted(path, processName))
            return;

        if (CodeSignVerifier.HasValidSignature(path))
            return;

        if (TryTerminate(pid))
            Interlocked.Increment(ref _terminated);
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
            catch { /* access denied — fall through */ }
        }
        catch
        {
            return null;
        }

        // SeDebugPrivilege + QUERY_LIMITED often works when MainModule does not.
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

    private static bool IsAllowlisted(string path, string processName)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var name = (processName ?? "").Trim().ToLowerInvariant();
            var file = Path.GetFileName(full);

            // Ourselves
            var self = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(self) &&
                full.Equals(Path.GetFullPath(self), StringComparison.OrdinalIgnoreCase))
                return true;

            if (file.Equals("YGuardAC.exe", StringComparison.OrdinalIgnoreCase))
                return true;

            // Windows OS trees
            var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrWhiteSpace(win) &&
                full.StartsWith(win.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                return true;

            // Installed software under Program Files — leave alone even if
            // Authenticode is missing/broken (drivers, OEM tools, etc.).
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            foreach (var root in new[] { pf, pf86 })
            {
                if (string.IsNullOrWhiteSpace(root)) continue;
                if (full.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Per-user "Programs" installs (Discord portable builds, etc.)
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(local))
            {
                var programs = Path.Combine(local, "Programs");
                if (full.StartsWith(programs + "\\", StringComparison.OrdinalIgnoreCase))
                    return true;

                foreach (var vendor in new[]
                         {
                             "Discord", "Microsoft", "Google", "Mozilla", "Packages",
                             "NVIDIA", "AMD", "Intel", "Steam", "EpicGamesLauncher",
                             "Riot Games", "Battle.net", "Ubisoft Game Launcher", "EADesktop",
                             "JetBrains", "cursor", "GitHubDesktop",
                         })
                {
                    var v = Path.Combine(local, vendor);
                    if (full.StartsWith(v + "\\", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            // Steam + CS2 stack (often mixed signing; never kill mid-match tooling)
            if (name is "steam" or "steamwebhelper" or "steamservice" or "gameoverlayui"
                or "cs2" or "csgo" or "steamerrorreporter" or "discord" or "discordptb"
                or "discordcanary" or "epicgameslauncher" or "origin" or "eadesktop"
                or "battle.net" or "agent" or "riotclientservices" or "vgtray")
                return true;

            if (full.Contains("\\Steam\\", StringComparison.OrdinalIgnoreCase) ||
                full.Contains("\\steamapps\\", StringComparison.OrdinalIgnoreCase) ||
                full.Contains("\\Epic Games\\", StringComparison.OrdinalIgnoreCase) ||
                full.Contains("\\Riot Games\\", StringComparison.OrdinalIgnoreCase) ||
                full.Contains("\\Battle.net\\", StringComparison.OrdinalIgnoreCase))
                return true;

            // NVIDIA / AMD overlays commonly used while gaming
            if (name.StartsWith("nv", StringComparison.Ordinal) ||
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
