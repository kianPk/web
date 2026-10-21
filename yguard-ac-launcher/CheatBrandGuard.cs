using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace YGuardAC;

/// <summary>
/// High-confidence product-brand detection for renamed externals.
/// Reports hits (ban path): process name, FileVersionInfo, window title.
/// </summary>
internal static class CheatBrandGuard
{
    private static readonly HashSet<string> SkipProcesses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "idle", "system", "smss", "csrss", "wininit", "winlogon", "services",
            "lsass", "svchost", "fontdrvhost", "dwm", "explorer", "sihost",
            "taskhostw", "runtimebroker", "searchhost", "shellexperiencehost",
            "textinputhost", "applicationframehost", "conhost", "cs2", "csgo",
            "steam", "steamwebhelper", "steamservice", "gameoverlayui",
            "yguardac", "yguard",
        };

    private static readonly string[] SkipPathMarkers =
    [
        @"\windows\system32\",
        @"\windows\syswow64\",
        @"\windows\winsxs\",
        @"\windowsapps\",
        @"\microsoft\windows\",
    ];

    public static void Scan(Action<CheatScanner.Hit> add)
    {
        try { ScanProcesses(add); } catch { /* never throw */ }
        try { ScanWindows(add); } catch { /* never throw */ }
    }

    public static bool ShouldKill(Process p)
    {
        try
        {
            var name = (p.ProcessName ?? "").Trim();
            if (name.Length == 0 || SkipProcesses.Contains(name))
                return false;
            if (CheatBrandTokens.Matches(name))
                return true;

            string? path = null;
            try { path = p.MainModule?.FileName; } catch { }
            if (string.IsNullOrWhiteSpace(path) || IsSkippedPath(path))
                return false;
            if (CheatBrandTokens.Matches(Path.GetFileName(path)))
                return true;
            return FileLooksBranded(path);
        }
        catch
        {
            return false;
        }
    }

    public static bool FileLooksBranded(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            if (CheatBrandTokens.Matches(Path.GetFileName(path)))
                return true;
            if (IsSkippedPath(path)) return false;
            var vi = FileVersionInfo.GetVersionInfo(path);
            var blob = string.Join(' ',
                vi.ProductName,
                vi.FileDescription,
                vi.InternalName,
                vi.OriginalFilename,
                vi.CompanyName,
                vi.Comments);
            return CheatBrandTokens.Matches(blob);
        }
        catch
        {
            return false;
        }
    }

    private static void ScanProcesses(Action<CheatScanner.Hit> add)
    {
        Process[] procs;
        try { procs = Process.GetProcesses(); }
        catch { return; }

        foreach (var p in procs)
        {
            try
            {
                var name = (p.ProcessName ?? "").Trim();
                if (name.Length == 0 || SkipProcesses.Contains(name))
                    continue;

                string? path = null;
                try { path = p.MainModule?.FileName; } catch { }

                var fromName = CheatBrandTokens.FirstMatch(name);
                if (fromName != null)
                {
                    add(new CheatScanner.Hit(CheatBrandTokens.Canonical(fromName), path, p.ProcessName));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(path) || IsSkippedPath(path))
                    continue;

                var fromFile = CheatBrandTokens.FirstMatch(Path.GetFileName(path));
                if (fromFile != null)
                {
                    add(new CheatScanner.Hit(CheatBrandTokens.Canonical(fromFile), path, p.ProcessName));
                    continue;
                }

                if (!FileLooksBranded(path))
                    continue;

                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(path);
                    var blob = string.Join(' ',
                        vi.ProductName, vi.FileDescription, vi.InternalName,
                        vi.OriginalFilename, vi.CompanyName);
                    var tok = CheatBrandTokens.FirstMatch(blob) ?? "memesense";
                    add(new CheatScanner.Hit(CheatBrandTokens.Canonical(tok), path, p.ProcessName));
                }
                catch { /* ignore */ }
            }
            catch { }
            finally
            {
                try { p.Dispose(); } catch { }
            }
        }
    }

    private static void ScanWindows(Action<CheatScanner.Hit> add)
    {
        EnumWindows((hWnd, _) =>
        {
            try
            {
                var title = GetWindowTitle(hWnd);
                if (string.IsNullOrWhiteSpace(title)) return true;

                var tok = CheatBrandTokens.FirstMatch(title);
                if (tok == null) return true;

                GetWindowThreadProcessId(hWnd, out var pid);
                if (pid <= 4) return true;

                string? procName = null;
                string? path = null;
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    procName = p.ProcessName;
                    if (!string.IsNullOrWhiteSpace(procName) && SkipProcesses.Contains(procName))
                        return true;
                    try { path = p.MainModule?.FileName; } catch { }
                }
                catch { return true; }

                add(new CheatScanner.Hit(CheatBrandTokens.Canonical(tok), path, procName));
            }
            catch { }
            return true;
        }, IntPtr.Zero);
    }

    private static bool IsSkippedPath(string path)
    {
        var p = path.Replace('/', '\\').ToLowerInvariant();
        return SkipPathMarkers.Any(m => p.Contains(m, StringComparison.Ordinal));
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        _ = GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString().Trim();
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
}
