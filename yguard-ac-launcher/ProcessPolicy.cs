using System.Diagnostics;

namespace YGuardAC;

/// <summary>
/// Shared process / path allowlist for CS2 session policy.
/// Identity is path markers + process name + (when available) Authenticode.
/// </summary>
internal static class ProcessPolicy
{
    public static readonly HashSet<string> AllowedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Game + Steam
        "cs2", "csgo", "steam", "steamwebhelper", "steamservice", "gameoverlayui",
        "steamerrorreporter", "steamservice", "steamclient",
        // YGuard
        "yguardac", "yguard",
        // Browsers / chat (user-requested companions)
        "chrome", "msedge", "firefox", "opera", "brave", "vivaldi",
        "telegram", "telegram desktop", "whatsapp", "discord", "discordptb", "discordcanary",
        "teams", "zoom", "skype",
        // GPU / capture / overlays (legitimate)
        "nvcontainer", "nvidia share", "nvidia overlay", "nvsphelper64", "nvcplui",
        "nvapp", "nvidiaapp", "nvidia broadcast",
        "amdow", "radeonsoftware", "amdrsserv",
        "rtss", "rivatuner", "msi afterburner", "encoder server", "hwinfo", "hwinfo64",
        "obs64", "obs32", "obs", "streamlabs obs", "streamlabs", "medal",
        "gamebar", "gamebarpresencewriter", "gamingservices", "xboxappservices",
        // Shell / OS
        "explorer", "dwm", "shellexperiencehost", "searchhost", "textinputhost",
        "applicationframehost", "systemsettings", "taskmgr", "conhost", "sihost",
        "runtimebroker", "svchost", "csrss", "services", "lsass", "smss", "wininit",
        "fontdrvhost", "ctfmon", "securityhealthservice",
        // Common peripherals
        "lghub", "icue", "armourycrate", "steelseriesgg", "razor synapse", "synapse",
        "wallpaperengine", "rainmeter", "powertoys", "sharex", "lightshot",
    };

    public static readonly string[] AllowedPathMarkers =
    [
        @"\windows\system32\",
        @"\windows\syswow64\",
        @"\windows\winsxs\",
        @"\windowsapps\",
        @"\microsoft\",
        @"\steam\",
        @"\steamapps\",
        @"\counter-strike global offensive\",
        @"\counter-strike 2\",
        @"\discord\",
        @"\obs-studio\",
        @"\nvidia\",
        @"\amd\",
        @"\ati technologies\",
        @"\intel\",
        @"\google\chrome\",
        @"\microsoft\edge\",
        @"\mozilla firefox\",
        @"\telegram desktop\",
        @"\yguard\",
    ];

    public static bool IsGameProcessName(string? name) =>
        string.Equals(name, "cs2", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "csgo", StringComparison.OrdinalIgnoreCase);

    public static bool IsAllowed(string? processName, string? path, bool requireTrustedSignatureForCompanions = false)
    {
        var name = (processName ?? "").Trim();
        if (string.IsNullOrEmpty(name))
            return false;

        if (pidIsSelf(path))
            return true;

        if (AllowedProcessNames.Contains(name))
        {
            if (!requireTrustedSignatureForCompanions)
                return true;
            // Companions on allowlist still preferred signed when path is available.
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return true;
            if (IsUnderAllowedPath(path))
                return true;
            try { return CodeSignVerifier.HasValidSignature(path); }
            catch { return false; }
        }

        if (!string.IsNullOrWhiteSpace(path) && IsUnderAllowedPath(path))
            return true;

        // Critical OS image names even if path lookup failed.
        if (name is "system" or "registry" or "memory compression" or "secure system")
            return true;

        return false;
    }

    public static bool IsUnderAllowedPath(string path)
    {
        try
        {
            var lower = Path.GetFullPath(path).ToLowerInvariant();
            var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrWhiteSpace(win) &&
                lower.StartsWith(win.TrimEnd('\\').ToLowerInvariant() + "\\", StringComparison.Ordinal))
                return true;

            foreach (var marker in AllowedPathMarkers)
            {
                if (lower.Contains(marker, StringComparison.Ordinal))
                    return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Process that should block CS2 entry until the user closes it.
    /// Conservative: unsigned + not allowlisted + not under OS/Steam/GPU paths.
    /// </summary>
    public static bool IsSessionBlocker(string? processName, string? path)
    {
        if (IsAllowed(processName, path))
            return false;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;
        try
        {
            if (CodeSignVerifier.HasValidSignature(path))
                return false;
        }
        catch { /* treat as unsigned */ }
        return true;
    }

    public static string? TryGetImagePath(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            try
            {
                var m = p.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(m))
                    return m;
            }
            catch { }
        }
        catch { }
        return null;
    }

    private static bool pidIsSelf(string? path)
    {
        try
        {
            var self = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(self) || string.IsNullOrWhiteSpace(path))
                return false;
            return Path.GetFullPath(self)
                .Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
