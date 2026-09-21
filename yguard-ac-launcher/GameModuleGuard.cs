using System.Diagnostics;
using System.Runtime.InteropServices;

namespace YGuardAC;

/// <summary>
/// Continuously enumerates modules in the CS2 process via
/// CreateToolhelp32Snapshot / Module32First / Module32Next.
/// Foreign DLLs that are not Windows, GPU drivers, Steam/CS2, and lack a
/// trusted Authenticode signature are unloaded (FreeLibrary) when possible;
/// if they persist the game process is closed. Never reports to the API —
/// local soft enforcement only (no platform ban).
/// </summary>
internal sealed class GameModuleGuard : IDisposable
{
    private readonly System.Threading.Timer _timer;
    private readonly object _gate = new();
    private readonly Dictionary<string, bool> _signatureCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _recentActions = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public int ClosedCount { get; private set; }

    public GameModuleGuard()
    {
        _timer = new System.Threading.Timer(
            _ => Tick(),
            null,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _timer.Dispose(); } catch { }
    }

    private void Tick()
    {
        if (_disposed) return;
        try
        {
            foreach (var p in Process.GetProcessesByName("cs2"))
            {
                try { ScanProcess(p.Id); }
                finally { try { p.Dispose(); } catch { } }
            }
        }
        catch { /* never throw from timer */ }
    }

    private void ScanProcess(int pid)
    {
        if (pid <= 0) return;

        var snap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, (uint)pid);
        if (snap == INVALID_HANDLE_VALUE || snap == IntPtr.Zero)
            return;

        try
        {
            var me = new MODULEENTRY32
            {
                dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>(),
            };

            if (!Module32First(snap, ref me))
                return;

            var bad = new List<(string Path, IntPtr Base)>();

            do
            {
                try
                {
                    var path = (me.szExePath ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(path))
                        continue;

                    // Main EXE is always expected.
                    var file = Path.GetFileName(path);
                    if (file.Equals("cs2.exe", StringComparison.OrdinalIgnoreCase) ||
                        file.Equals("csgo.exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (IsAllowlistedModule(path))
                        continue;

                    if (HasTrustedSignatureCached(path))
                        continue;

                    bad.Add((path, me.modBaseAddr));
                }
                catch { /* keep walking */ }
            }
            while (Module32Next(snap, ref me));

            foreach (var (path, bas) in bad)
            {
                HandleForeignModule(pid, path, bas);
            }
        }
        finally
        {
            CloseHandle(snap);
        }
    }

    private void HandleForeignModule(int pid, string path, IntPtr moduleBase)
    {
        lock (_gate)
        {
            var key = $"{pid}|{path}";
            if (!_recentActions.Add(key))
                return;
            if (_recentActions.Count > 500)
                _recentActions.Clear();
        }

        // Prefer unloading the DLL from the game address space.
        var freed = TryRemoteFreeLibrary(pid, moduleBase);
        if (freed)
        {
            ClosedCount++;
            return;
        }

        // Unload failed or module is sticky — close the game only (no ban).
        try
        {
            using var p = Process.GetProcessById(pid);
            try { p.Kill(entireProcessTree: true); }
            catch
            {
                try { p.Kill(); } catch { }
            }
            ClosedCount++;
        }
        catch { /* already exited */ }
    }

    private static bool TryRemoteFreeLibrary(int pid, IntPtr moduleBase)
    {
        if (moduleBase == IntPtr.Zero) return false;

        const uint PROCESS_CREATE_THREAD = 0x0002;
        const uint PROCESS_QUERY_INFORMATION = 0x0400;
        const uint PROCESS_VM_OPERATION = 0x0008;
        const uint PROCESS_VM_WRITE = 0x0020;
        const uint PROCESS_VM_READ = 0x0010;
        const uint access = PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION
                            | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ;

        var hProcess = OpenProcess(access, false, (uint)pid);
        if (hProcess == IntPtr.Zero)
            return false;

        try
        {
            var hKernel = GetModuleHandle("kernel32.dll");
            if (hKernel == IntPtr.Zero) return false;

            var freeLibrary = GetProcAddress(hKernel, "FreeLibrary");
            if (freeLibrary == IntPtr.Zero) return false;

            var thread = CreateRemoteThread(
                hProcess,
                IntPtr.Zero,
                UIntPtr.Zero,
                freeLibrary,
                moduleBase,
                0,
                out _);
            if (thread == IntPtr.Zero)
                return false;

            try
            {
                WaitForSingleObject(thread, 2000);
                return true;
            }
            finally
            {
                CloseHandle(thread);
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    private bool HasTrustedSignatureCached(string path)
    {
        lock (_gate)
        {
            if (_signatureCache.TryGetValue(path, out var cached))
                return cached;
        }

        var ok = false;
        try { ok = CodeSignVerifier.HasValidSignature(path); }
        catch { ok = false; }

        lock (_gate)
        {
            if (_signatureCache.Count > 2000)
                _signatureCache.Clear();
            _signatureCache[path] = ok;
        }

        return ok;
    }

    private static bool IsAllowlistedModule(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var lower = full.ToLowerInvariant();

            // Windows
            var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrWhiteSpace(win) &&
                full.StartsWith(win.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                return true;

            // Steam + CS2 game trees
            if (lower.Contains("\\steam\\") ||
                lower.Contains("\\steamapps\\") ||
                lower.Contains("\\counter-strike") ||
                lower.Contains("\\cs2\\") ||
                lower.Contains("\\game\\bin\\") ||
                lower.Contains("\\game\\csgo\\"))
                return true;

            // Official GPU / display stacks
            if (lower.Contains("\\nvidia\\") ||
                lower.Contains("\\amd\\") ||
                lower.Contains("\\ati\\") ||
                lower.Contains("\\radeon\\") ||
                lower.Contains("\\intel\\") ||
                lower.Contains("\\wow64_microsoft.windows.gdi") ||
                lower.Contains("\\drivers\\"))
                return true;

            // Common signed overlay hosts that inject into games
            if (lower.Contains("\\discord\\") ||
                lower.Contains("\\obs-studio\\") ||
                lower.Contains("\\overwolf\\") ||
                lower.Contains("\\rtiostreaming\\") ||
                lower.Contains("\\rivatuner\\") ||
                lower.Contains("\\msi afterburner\\") ||
                lower.Contains("\\microsoft\\edgegameassist\\") ||
                lower.Contains("\\xboxgames\\") ||
                lower.Contains("\\gamebar\\"))
                return true;
        }
        catch { /* fall through */ }

        return false;
    }

    private const uint TH32CS_SNAPMODULE = 0x00000008;
    private const uint TH32CS_SNAPMODULE32 = 0x00000010;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateRemoteThread(
        IntPtr hProcess,
        IntPtr lpThreadAttributes,
        UIntPtr dwStackSize,
        IntPtr lpStartAddress,
        IntPtr lpParameter,
        uint dwCreationFlags,
        out uint lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
}
