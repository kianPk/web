using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace YGuardAC;

/// <summary>
/// When CS2 is running, close processes that own cheat menus.
/// DH-style UIs often have an empty title bar — we scan child control text
/// for Aimbot / Visuals / ESP / etc. Local only — never bans.
/// </summary>
internal static class CheatUiGuard
{
    private static readonly string[] SuspiciousParts =
    [
        "aimbot", "wallhack", "ragebot", "legitbot", "triggerbot",
        "neverlose", "fatality", "onetap", "nixware", "gamesense", "skeet",
        "exloader", "primordial", "interium", "rawetrip", "spirthack",
        "cs-go2", "csgo2", "cs2 cheat", "esp menu",
        "visuals", "misc", "snap line", "eye ray", "visible check",
        "maximum render distance", "box rounding", "health bar",
        "armor bar", "skeleton",
    ];

    /// <summary>
    /// Strong signal: several of these together = external cheat menu (DH).
    /// </summary>
    private static readonly string[] StrongCheatUiTokens =
    [
        "aimbot", "visuals", "esp", "ragebot", "legitbot", "triggerbot",
        "wallhack", "snap line", "eye ray",
    ];

    private static readonly HashSet<string> ProcAllow = new(StringComparer.OrdinalIgnoreCase)
    {
        "cs2", "csgo", "steam", "steamwebhelper", "steamservice", "gameoverlayui",
        "discord", "discordptb", "discordcanary", "chrome", "msedge", "firefox",
        "opera", "brave", "vivaldi",
        "obs64", "obs32", "obs", "yguardac", "explorer", "dwm", "textinputhost",
        "applicationframehost", "searchhost", "shellexperiencehost",
        "code", "cursor", "devenv", "notepad", "notepad++",
    };

    public static int ScanAndClose()
    {
        if (Process.GetProcessesByName("cs2").Length == 0 &&
            Process.GetProcessesByName("csgo").Length == 0)
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
                if (w < 320 || h < 240) return true;

                GetWindowThreadProcessId(hWnd, out var pidU);
                var pid = (int)pidU;
                if (pid <= 4 || !pids.Add(pid)) return true;

                string? name;
                try
                {
                    using var p = Process.GetProcessById(pid);
                    name = p.ProcessName;
                }
                catch { return true; }

                if (string.IsNullOrWhiteSpace(name) || ProcAllow.Contains(name))
                    return true;

                var blob = CollectWindowTextBlob(hWnd);
                if (!LooksLikeCheatUi(blob))
                    return true;

                try
                {
                    using var p = Process.GetProcessById(pid);
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

    private static bool LooksLikeCheatUi(string blob)
    {
        if (string.IsNullOrWhiteSpace(blob)) return false;
        var t = blob.ToLowerInvariant();

        // Single hard hits
        if (SuspiciousParts.Any(p => p.Length >= 6 && t.Contains(p)))
        {
            // Require at least one strong token so random apps aren't nuked.
            var strong = StrongCheatUiTokens.Count(tok => t.Contains(tok));
            if (strong >= 2) return true;
            if (t.Contains("aimbot") || t.Contains("wallhack") || t.Contains("neverlose")
                || t.Contains("fatality") || t.Contains("onetap") || t.Contains("exloader"))
                return true;
        }

        // DH layout: Visuals + Aimbot + Misc tabs often present together
        var hits = 0;
        if (t.Contains("visuals")) hits++;
        if (t.Contains("aimbot")) hits++;
        if (t.Contains("misc")) hits++;
        if (t.Contains("esp")) hits++;
        if (t.Contains("cloud")) hits++;
        return hits >= 3;
    }

    private static string CollectWindowTextBlob(IntPtr root)
    {
        var sb = new StringBuilder(4096);
        AppendText(root, sb);
        EnumChildWindows(root, (child, _) =>
        {
            AppendText(child, sb);
            return sb.Length < 8000;
        }, IntPtr.Zero);
        return sb.ToString();
    }

    private static void AppendText(IntPtr hWnd, StringBuilder dest)
    {
        try
        {
            var len = GetWindowTextLength(hWnd);
            if (len <= 0 || len > 512) return;
            var buf = new StringBuilder(len + 2);
            _ = GetWindowText(hWnd, buf, buf.Capacity);
            var s = buf.ToString().Trim();
            if (s.Length == 0) return;
            dest.Append(' ').Append(s);
        }
        catch { }
    }

    private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

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
