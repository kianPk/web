using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace YGuardAC;

/// <summary>
/// Closes click-through layered overlays that sit on top of CS2.
/// Conservative: never reports hits for bans — only closes the window.
/// Requires layered + transparent + topmost and near-exact game size.
/// </summary>
internal static class OverlayGuard
{
    private const int GwlExStyle = -20;
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExTopmost = 0x00000008;
    private const uint WmClose = 0x0010;
    private const int SizeTolerancePx = 4;

    private static readonly HashSet<string> Allowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        "cs2", "csgo", "steam", "steamwebhelper", "steamservice", "gameoverlayui",
        "discord", "discordptb", "discordcanary",
        "nvcontainer", "nvidia share", "nvidia overlay", "nvsphelper64", "nvcplui",
        "nvapp", "nvidiaapp", "nvidia broadcast",
        "amdow", "radeonsoftware", "amdrsserv", "amdow",
        "rtss", "rivatuner", "msi afterburner", "encoder server", "hwinfo", "hwinfo64",
        "obs64", "obs32", "obs", "streamlabs obs", "streamlabs",
        "gamebar", "gamebarpresencewriter", "gamingservices", "xboxappservices",
        "explorer", "dwm", "shellexperiencehost", "searchhost", "textinputhost",
        "applicationframehost", "systemsettings", "taskmgr", "conhost", "sihost",
        "yguardac", "yguard",
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi",
        "wallpaperengine", "rainmeter", "powertoys", "sharex", "lightshot",
        "medal", "overwolf", "curseforge", "lghub", "icue", "armourycrate",
        "asus", "steelseries", "razor", "synapse", "corsair",
        "teams", "zoom", "skype", "telegram", "whatsapp",
    };

    private static readonly string[] AllowedPathMarkers =
    [
        @"\windows\system32\",
        @"\windows\syswow64\",
        @"\windowsapps\",
        @"\microsoft\",
        @"\steam\",
        @"\discord\",
        @"\obs-studio\",
        @"\nvidia\",
        @"\amd\",
    ];

    /// <summary>
    /// Close suspicious overlays. Does <b>not</b> return ban-worthy hits —
    /// callers must not feed this into the cheat report.
    /// </summary>
    public static int ScanAndClose()
    {
        var closed = 0;
        if (!TryGetGameBounds(out var gameRect, out var gamePid))
            return 0;

        var gameW = gameRect.Right - gameRect.Left;
        var gameH = gameRect.Bottom - gameRect.Top;
        // Borderless/fullscreen overlays only — ignore tiny tool windows.
        if (gameW < 800 || gameH < 600)
            return 0;

        EnumWindows((hWnd, _) =>
        {
            try
            {
                if (!IsWindowVisible(hWnd)) return true;
                if (!GetWindowRect(hWnd, out var rect)) return true;

                var w = rect.Right - rect.Left;
                var h = rect.Bottom - rect.Top;
                if (Math.Abs(w - gameW) > SizeTolerancePx || Math.Abs(h - gameH) > SizeTolerancePx)
                    return true;
                // Must also cover the same screen region (not just same size elsewhere).
                if (Math.Abs(rect.Left - gameRect.Left) > SizeTolerancePx ||
                    Math.Abs(rect.Top - gameRect.Top) > SizeTolerancePx)
                    return true;

                var ex = GetWindowLong(hWnd, GwlExStyle);
                var layered = (ex & WsExLayered) != 0;
                var transparent = (ex & WsExTransparent) != 0;
                var topmost = (ex & WsExTopmost) != 0;
                // Real external WH overlays are click-through + layered + topmost.
                // Requiring all three avoids Discord/NVIDIA false positives.
                if (!layered || !transparent || !topmost)
                    return true;

                GetWindowThreadProcessId(hWnd, out var pid);
                if (pid == 0 || pid == gamePid) return true;

                string? procName;
                string? path = null;
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    procName = p.ProcessName;
                    try { path = p.MainModule?.FileName; } catch { /* access denied */ }
                }
                catch { return true; }

                if (string.IsNullOrWhiteSpace(procName)) return true;
                if (IsAllowlisted(procName, path)) return true;

                // Named windows are usually legitimate UI; WH overlays are blank.
                var title = GetWindowTitle(hWnd);
                if (!string.IsNullOrEmpty(title))
                    return true;

                // Soft close only — never Kill (avoids nuking innocent apps).
                try { PostMessage(hWnd, WmClose, IntPtr.Zero, IntPtr.Zero); } catch { }
                closed++;
            }
            catch { /* keep enumerating */ }
            return true;
        }, IntPtr.Zero);

        return closed;
    }

    private static bool IsAllowlisted(string processName, string? path)
    {
        var n = processName.Trim().ToLowerInvariant();
        if (Allowlist.Contains(n)) return true;
        if (Allowlist.Any(a => n.Contains(a))) return true;
        if (!string.IsNullOrWhiteSpace(path))
        {
            var p = path.Replace('/', '\\').ToLowerInvariant();
            if (AllowedPathMarkers.Any(m => p.Contains(m)))
                return true;
        }
        return false;
    }

    private static bool TryGetGameBounds(out Rect rect, out uint gamePid)
    {
        rect = default;
        gamePid = 0;
        IntPtr found = IntPtr.Zero;
        uint foundPid = 0;
        Rect foundRect = default;

        EnumWindows((hWnd, _) =>
        {
            try
            {
                if (!IsWindowVisible(hWnd)) return true;
                GetWindowThreadProcessId(hWnd, out var pid);
                if (pid == 0) return true;
                string? name;
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    name = p.ProcessName;
                }
                catch { return true; }

                if (!string.Equals(name, "cs2", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(name, "csgo", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (!GetWindowRect(hWnd, out var r)) return true;
                var w = r.Right - r.Left;
                var h = r.Bottom - r.Top;
                if (w < 800 || h < 600) return true;

                var curArea = (foundRect.Right - foundRect.Left) * (foundRect.Bottom - foundRect.Top);
                var newArea = w * h;
                if (found == IntPtr.Zero || newArea > curArea)
                {
                    found = hWnd;
                    foundPid = pid;
                    foundRect = r;
                }
            }
            catch { }
            return true;
        }, IntPtr.Zero);

        if (found == IntPtr.Zero) return false;
        rect = foundRect;
        gamePid = foundPid;
        return true;
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
