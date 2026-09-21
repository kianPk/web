using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace YGuardAC;

/// <summary>
/// When CS2 is running, close processes that own obvious cheat UI windows
/// (title / class heuristics). Local only — never bans.
/// Catches external menus that sneak past Authenticode (stolen certs).
/// </summary>
internal static class CheatUiGuard
{
    private static readonly string[] SuspiciousTitleParts =
    [
        "aimbot", "wallhack", "ragebot", "legitbot", "triggerbot",
        "neverlose", "fatality", "onetap", "nixware", "gamesense", "skeet",
        "exloader", "primordial", "interium", "rawetrip", "spirthack",
        "cs-go2", "csgo2", "cs2 cheat", "esp menu", "wallhack",
    ];

    private static readonly HashSet<string> ProcAllow = new(StringComparer.OrdinalIgnoreCase)
    {
        "cs2", "csgo", "steam", "steamwebhelper", "steamservice", "gameoverlayui",
        "discord", "discordptb", "discordcanary", "chrome", "msedge", "firefox",
        "obs64", "obs32", "obs", "yguardac", "explorer", "dwm", "textinputhost",
        "applicationframehost", "searchhost", "shellexperiencehost",
    };

    public static int ScanAndClose()
    {
        if (Process.GetProcessesByName("cs2").Length == 0)
            return 0;

        var closed = 0;
        var pids = new HashSet<int>();

        EnumWindows((hWnd, _) =>
        {
            try
            {
                if (!IsWindowVisible(hWnd)) return true;
                if (!GetWindowRect(hWnd, out var rect)) return true;
                var w = rect.Right - rect.Left;
                var h = rect.Bottom - rect.Top;
                // Cheat menus are real windows, not tiny toasts.
                if (w < 280 || h < 200) return true;

                var title = GetTitle(hWnd);
                if (string.IsNullOrWhiteSpace(title)) return true;
                var t = title.ToLowerInvariant();
                if (!SuspiciousTitleParts.Any(p => t.Contains(p)))
                    return true;

                GetWindowThreadProcessId(hWnd, out var pid);
                if (pid == 0 || !pids.Add((int)pid)) return true;

                string? name = null;
                string? path = null;
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    name = p.ProcessName;
                    try { path = p.MainModule?.FileName; } catch { }
                }
                catch { return true; }

                if (string.IsNullOrWhiteSpace(name) || ProcAllow.Contains(name))
                    return true;

                // If it's a known browser with "visuals" in a tab title — skip.
                if (name is "chrome" or "msedge" or "firefox" or "opera" or "brave")
                    return true;

                // Soft preference: unsigned → always kill. Signed but suspicious title → kill too
                // (stolen certs on external menus).
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    try { p.Kill(entireProcessTree: true); }
                    catch { try { p.Kill(); } catch { } }
                    closed++;
                }
                catch { }
            }
            catch { }
            return true;
        }, IntPtr.Zero);

        return closed;
    }

    private static string GetTitle(IntPtr hWnd)
    {
        var sb = new StringBuilder(512);
        _ = GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString().Trim();
    }

    private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }
}
