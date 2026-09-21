using System.Diagnostics;

namespace YGuardAC;

/// <summary>
/// Client-side signatures for known CS cheat loaders / install trees.
/// Hits are reported to the API (ban path). <see cref="Remediate"/> only
/// kills matching processes and deletes paths under known cheat roots —
/// never random user/system files.
///
/// Folder-name-only hits are limited to high-confidence unique names.
/// Generic words (Midnight, Nemesis, Osiris, skeet, …) need a known
/// cheat binary inside — otherwise innocent folders cause mass bans.
/// </summary>
internal static class CheatScanner
{
    public sealed record Hit(string Signature, string? Path, string? ProcessName);

    /// <summary>
    /// Exact process names only (case-insensitive). Never use short substrings
    /// like "loader" / "hack" — those false-positive on legitimate software.
    /// </summary>
    private static readonly Dictionary<string, string> ExactProcesses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["exloader"] = "exloader",
            ["exloaderui"] = "exloader",
            ["ex-loader"] = "exloader",
            ["neverlose"] = "neverlose",
            ["neverlose_loader"] = "neverlose",
            ["nl_loader"] = "neverlose",
            ["fatality"] = "fatality",
            ["fatality_loader"] = "fatality",
            ["nixware"] = "nixware",
            ["nixware_loader"] = "nixware",
            ["primordial"] = "primordial",
            ["primordial_loader"] = "primordial",
            ["gamesense"] = "gamesense",
            ["skeet"] = "gamesense",
            ["onetap"] = "onetap",
            ["onetapv4"] = "onetap",
            ["otc3"] = "onetap",
            ["aimjunkies"] = "aimjunkies",
            ["ajloader"] = "aimjunkies",
            ["spirt"] = "spirt",
            ["spirthack"] = "spirt",
            ["interium"] = "interium",
            ["interiumloader"] = "interium",
            ["midnight"] = "midnight",
            ["midnight_loader"] = "midnight",
            ["nemesis"] = "nemesis",
            ["nemesisloader"] = "nemesis",
            ["rawetrip"] = "rawetrip",
            ["osiris"] = "osiris",
            ["cheatengine-x86_64"] = "cheatengine",
            ["cheatengine-i386"] = "cheatengine",
            ["cheatengine"] = "cheatengine",
        };

    /// <summary>
    /// Process names that are also common for non-cheat software. Require a
    /// path under a known cheat root or a known cheat filename — never ban
    /// on the bare process name alone.
    /// </summary>
    private static readonly HashSet<string> AmbiguousProcessNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "midnight",
            "nemesis",
            "skeet",
            "osiris",
            "gamesense",
            "spirt",
            "interium",
            "primordial",
        };

    /// <summary>
    /// High-confidence install folders — unique enough that existence alone
    /// is a hit (typical cheat product directories).
    /// </summary>
    private static readonly (string Folder, string Signature)[] UniqueInstallFolders =
    [
        ("ExLoader", "exloader"),
        ("Neverlose", "neverlose"),
        ("neverlose", "neverlose"),
        ("Fatality", "fatality"),
        ("fatality", "fatality"),
        ("Nixware", "nixware"),
        ("nixware", "nixware"),
        ("Primordial", "primordial"),
        ("OneTap", "onetap"),
        ("onetap", "onetap"),
        ("Onetap", "onetap"),
        ("Aimjunkies", "aimjunkies"),
        ("aimjunkies", "aimjunkies"),
        ("Interium", "interium"),
        ("Rawetrip", "rawetrip"),
        ("NemesisCS", "nemesis"),
        ("spirthack", "spirt"),
        ("SpirtHack", "spirt"),
        ("neverlose_loader", "neverlose"),
        ("fatality_loader", "fatality"),
    ];

    /// <summary>
    /// Ambiguous folder names — only hit when a known cheat binary is inside.
    /// </summary>
    private static readonly (string Folder, string Signature)[] AmbiguousInstallFolders =
    [
        ("Nemesis", "nemesis"),
        ("nemesis", "nemesis"),
        ("Midnight", "midnight"),
        ("midnight", "midnight"),
        ("Osiris", "osiris"),
        ("osiris", "osiris"),
        ("skeet", "gamesense"),
        ("Skeet", "gamesense"),
        ("Spirt", "spirt"),
        ("gamesense", "gamesense"),
        ("Gamesense", "gamesense"),
        ("primordial", "primordial"),
    ];

    private static readonly string[] CheatFilesInsideRoots =
    [
        "ExLoader.exe",
        "exloader.exe",
        "ExLoader.dll",
        "neverlose.exe",
        "neverlose_loader.exe",
        "nl_loader.exe",
        "fatality.exe",
        "fatality_loader.exe",
        "nixware.exe",
        "nixware_loader.exe",
        "primordial.exe",
        "primordial_loader.exe",
        "gamesense.exe",
        "skeet.exe",
        "onetap.exe",
        "onetapv4.exe",
        "otc3.exe",
        "aimjunkies.exe",
        "ajloader.exe",
        "spirthack.exe",
        "interium.exe",
        "interiumloader.exe",
        "midnight.exe",
        "midnight_loader.exe",
        "nemesis.exe",
        "nemesisloader.exe",
        "rawetrip.exe",
        "osiris.exe",
        "cheatengine-x86_64.exe",
        "cheatengine.exe",
    ];

    private static readonly string[] SearchRoots = BuildSearchRoots();

    public static List<Hit> Scan()
    {
        var hits = new List<Hit>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(Hit hit)
        {
            var key = $"{hit.Signature}|{hit.Path ?? ""}|{hit.ProcessName ?? ""}";
            if (seen.Add(key))
                hits.Add(hit);
        }

        try { ScanInstallTrees(Add); } catch { /* never throw out of Scan */ }
        try { ScanProcesses(Add); } catch { /* ignore */ }

        return hits;
    }

    public static int Remediate(IReadOnlyList<Hit> hits)
    {
        if (hits.Count == 0) return 0;

        KillMatchingProcesses();

        var allowedRoots = CollectKnownCheatRoots();
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hit in hits)
        {
            if (string.IsNullOrWhiteSpace(hit.Path)) continue;
            if (!IsUnderAllowedRoot(hit.Path, allowedRoots)) continue;
            try
            {
                if (File.Exists(hit.Path))
                    files.Add(hit.Path);
                else if (Directory.Exists(hit.Path))
                    dirs.Add(hit.Path);
            }
            catch { /* ignore */ }
        }

        foreach (var root in allowedRoots)
        {
            try
            {
                if (Directory.Exists(root))
                    dirs.Add(root);
            }
            catch { /* ignore */ }
        }

        var removed = 0;

        foreach (var file in files)
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                removed++;
            }
            catch { /* locked */ }
        }

        foreach (var dir in dirs.OrderByDescending(d => d.Length))
        {
            try
            {
                if (!Directory.Exists(dir)) continue;
                if (!IsUnderAllowedRoot(dir, allowedRoots) && !allowedRoots.Contains(dir))
                    continue;

                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(f, FileAttributes.Normal);
                        File.Delete(f);
                    }
                    catch { /* ignore */ }
                }
                Directory.Delete(dir, recursive: true);
                removed++;
            }
            catch { /* access denied */ }
        }

        return removed;
    }

    private static void ScanInstallTrees(Action<Hit> add)
    {
        foreach (var root in SearchRoots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            foreach (var (folder, signature) in UniqueInstallFolders)
                TryHitFolder(root, folder, signature, requireBinary: false, add);

            foreach (var (folder, signature) in AmbiguousInstallFolders)
                TryHitFolder(root, folder, signature, requireBinary: true, add);
        }
    }

    private static void TryHitFolder(
        string root,
        string folder,
        string signature,
        bool requireBinary,
        Action<Hit> add)
    {
        string full;
        try { full = Path.Combine(root, folder); }
        catch { return; }

        try
        {
            if (!Directory.Exists(full)) return;

            var hasBinary = FolderContainsKnownCheatBinary(full);
            if (requireBinary && !hasBinary)
                return;

            // Unique folders: existence is enough. Still prefer reporting a
            // concrete binary path when present (cleaner remediations).
            if (hasBinary)
            {
                foreach (var name in CheatFilesInsideRoots)
                {
                    try
                    {
                        var file = Path.Combine(full, name);
                        if (File.Exists(file))
                            add(new Hit(signature, file, null));
                    }
                    catch { /* ignore */ }
                }

                try
                {
                    foreach (var f in Directory.EnumerateFiles(full, "*.exe", SearchOption.AllDirectories)
                                 .Take(20))
                    {
                        if (IsKnownCheatFileName(f))
                            add(new Hit(signature, f, null));
                    }
                }
                catch { /* partial ACL */ }
            }
            else
            {
                add(new Hit(signature, full, null));
            }
        }
        catch { /* ignore */ }
    }

    private static bool FolderContainsKnownCheatBinary(string folder)
    {
        try
        {
            foreach (var name in CheatFilesInsideRoots)
            {
                try
                {
                    if (File.Exists(Path.Combine(folder, name)))
                        return true;
                }
                catch { /* ignore */ }
            }

            foreach (var f in Directory.EnumerateFiles(folder, "*.exe", SearchOption.AllDirectories)
                         .Take(40))
            {
                if (IsKnownCheatFileName(f))
                    return true;
            }
        }
        catch { /* ignore */ }

        return false;
    }

    private static void ScanProcesses(Action<Hit> add)
    {
        Process[] procs;
        try { procs = Process.GetProcesses(); }
        catch { return; }

        var allowedRoots = CollectKnownCheatRoots();

        foreach (var p in procs)
        {
            try
            {
                var name = (p.ProcessName ?? "").Trim();
                if (name.Length == 0) continue;

                if (!ExactProcesses.TryGetValue(name, out var signature))
                    continue;

                string? path = null;
                try { path = p.MainModule?.FileName; } catch { /* access denied */ }

                var ambiguous = AmbiguousProcessNames.Contains(name);

                if (ambiguous)
                {
                    // No path / unknown path → do not ban (too many false positives).
                    if (string.IsNullOrWhiteSpace(path))
                        continue;
                    if (!IsUnderAllowedRoot(path, allowedRoots) && !IsKnownCheatFileName(path))
                        continue;
                }
                else if (!string.IsNullOrWhiteSpace(path))
                {
                    if (!IsUnderAllowedRoot(path, allowedRoots) && !IsKnownCheatFileName(path))
                    {
                        // High-confidence process name (exloader, neverlose, …)
                        // still counts even outside known roots.
                        add(new Hit(signature, null, p.ProcessName));
                        continue;
                    }
                }

                add(new Hit(signature, path, p.ProcessName));
            }
            catch { }
            finally
            {
                try { p.Dispose(); } catch { }
            }
        }
    }

    private static void KillMatchingProcesses()
    {
        Process[] procs;
        try { procs = Process.GetProcesses(); }
        catch { return; }

        var allowedRoots = CollectKnownCheatRoots();

        foreach (var p in procs)
        {
            try
            {
                var name = (p.ProcessName ?? "").Trim();
                if (name.Length == 0) continue;
                if (!ExactProcesses.ContainsKey(name)) continue;

                if (AmbiguousProcessNames.Contains(name))
                {
                    string? path = null;
                    try { path = p.MainModule?.FileName; } catch { }
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    if (!IsUnderAllowedRoot(path, allowedRoots) && !IsKnownCheatFileName(path))
                        continue;
                }

                try { p.Kill(entireProcessTree: true); }
                catch
                {
                    try { p.Kill(); } catch { }
                }
                try { p.WaitForExit(2000); } catch { }
            }
            catch { }
            finally
            {
                try { p.Dispose(); } catch { }
            }
        }
    }

    private static HashSet<string> CollectKnownCheatRoots()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in SearchRoots)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            void Consider((string Folder, string Signature) entry, bool requireBinary)
            {
                try
                {
                    var full = Path.Combine(root, entry.Folder);
                    if (!Directory.Exists(full)) return;
                    if (requireBinary && !FolderContainsKnownCheatBinary(full)) return;
                    set.Add(Path.GetFullPath(full));
                }
                catch { /* ignore */ }
            }

            foreach (var e in UniqueInstallFolders)
                Consider(e, requireBinary: false);
            foreach (var e in AmbiguousInstallFolders)
                Consider(e, requireBinary: true);
        }
        return set;
    }

    private static bool IsUnderAllowedRoot(string path, HashSet<string> roots)
    {
        if (roots.Count == 0) return false;
        string full;
        try { full = Path.GetFullPath(path); }
        catch { return false; }

        foreach (var root in roots)
        {
            try
            {
                var r = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        + Path.DirectorySeparatorChar;
                if (full.Equals(root, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (full.StartsWith(r, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { /* ignore */ }
        }
        return false;
    }

    private static bool IsKnownCheatFileName(string path)
    {
        string name;
        try { name = Path.GetFileName(path); }
        catch { return false; }
        return CheatFilesInsideRoots.Any(f =>
            f.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] BuildSearchRoots()
    {
        var list = new List<string>
        {
            @"C:\Program Files",
            @"C:\Program Files (x86)",
        };

        void AddSpecial(Environment.SpecialFolder folder)
        {
            try
            {
                var p = Environment.GetFolderPath(folder);
                if (!string.IsNullOrWhiteSpace(p))
                    list.Add(p);
            }
            catch { /* ignore */ }
        }

        // Prefer cheat-typical trees — not the entire user profile (too noisy).
        AddSpecial(Environment.SpecialFolder.LocalApplicationData);
        AddSpecial(Environment.SpecialFolder.ApplicationData);
        AddSpecial(Environment.SpecialFolder.CommonApplicationData);
        AddSpecial(Environment.SpecialFolder.DesktopDirectory);
        AddSpecial(Environment.SpecialFolder.MyDocuments);

        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (!string.IsNullOrWhiteSpace(desktop) && Directory.Exists(desktop))
            {
                foreach (var sub in Directory.EnumerateDirectories(desktop).Take(40))
                    list.Add(sub);
            }
        }
        catch { /* ignore */ }

        return list
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
