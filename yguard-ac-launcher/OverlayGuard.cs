using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace YGuardAC;

/// <summary>
/// Detects external wallhack-style overlays: layered / click-through topmost
/// windows that match the CS2 client size, then closes them.
/// </summary>
internal static class OverlayGuard
{
    private const int GwlExStyle = -20;
    private const int WsExLayered = 0x00080000;
    private const int WsExTransparent = 0x00000020;
    private const int WsExTopmost = 0x00000008;
    private const uint WmClose = 0x0010;
    private const int SizeTolerancePx = 8;

    private static readonly HashSet<string> Allowlist = new(StringComparer.OrdinalIgnoreCase)
    {
        "cs2", "csgo", "steam", "steamwebhelper", "steamservice", "gameoverlayui",
        "discord", "discordptb", "discordcanary",
        "nvcontainer", "nvidia share", "nvidia overlay", "nvsphelper64", "nvcplui",
        "rtss", "rivatuner", "msi afterburner", "encoder server",
        "obs64", "obs32", "obs", "streamlabs obs",
        "gamebar", "gamebarpresencewriter", "gamingservices", "xboxappservices",
        "explorer", "dwm", "shellexperiencehost", "searchhost", "textinputhost",
        "applicationframehost", "systemsettings", "taskmgr",
        "yguardac", "yguard",
        "chrome", "msedge", "firefox", "opera", "brave",
        "wallpaperengine", "rainmeter",
    };

    public sealed record Hit(string Signature, string? Path, string? ProcessName);

    public static List<Hit> ScanAndClose()
    {
        var hits = new List<Hit>();
        if (!TryGetGameBounds(out var gameRect, out var gamePid))
            return hits;

        var gameW = gameRect.Right - gameRect.Left;
        var gameH = gameRect.Bottom - gameRect.Top;
        if (gameW < 320 || gameH < 240)
            return hits;

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

                var ex = GetWindowLong(hWnd, GwlExStyle);
                var layered = (ex & WsExLayered) != 0;
                var transparent = (ex & WsExTransparent) != 0;
                var topmost = (ex & WsExTopmost) != 0;
                // External WH overlays are almost always layered + (transparent and/or topmost).
                if (!layered || (!transparent && !topmost))
                    return true;

                GetWindowThreadProcessId(hWnd, out var pid);
                if (pid == 0 || pid == gamePid) return true;

                string? procName = null;
                string? path = null;
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    procName = p.ProcessName;
                    try { path = p.MainModule?.FileName; } catch { /* access denied */ }
                }
                catch { return true; }

                if (string.IsNullOrWhiteSpace(procName)) return true;
                if (IsAllowlisted(procName)) return true;

                // Empty / junk titles are common for injector overlays; titled
                // system apps are usually allowlisted already.
                var title = GetWindowTitle(hWnd);
                if (!string.IsNullOrEmpty(title) && title.Length > 64)
                    return true;

                hits.Add(new Hit("external_overlay", path, procName));
                CloseOverlay(hWnd, (int)pid);
            }
            catch { /* keep enumerating */ }
            return true;
        }, IntPtr.Zero);

        return hits
            .GroupBy(h => (h.Signature, h.Path ?? "", h.ProcessName ?? ""))
            .Select(g => g.First())
            .ToList();
    }

    private static bool IsAllowlisted(string processName)
    {
        var n = processName.Trim().ToLowerInvariant();
        if (Allowlist.Contains(n)) return true;
        return Allowlist.Any(a => n.Contains(a));
    }

    private static void CloseOverlay(IntPtr hWnd, int pid)
    {
        try { PostMessage(hWnd, WmClose, IntPtr.Zero, IntPtr.Zero); } catch { }
        try
        {
            // Force-close if it ignores WM_CLOSE (many overlays do).
            using var p = Process.GetProcessById(pid);
            if (!p.HasExited)
            {
                try { p.Kill(entireProcessTree: true); } catch { try { p.Kill(); } catch { } }
            }
        }
        catch { }
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
                if (w < 320 || h < 240) return true;

                // Prefer the largest visible game window (main client).
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
