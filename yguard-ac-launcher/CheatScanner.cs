using System.Diagnostics;

namespace YGuardAC;

/// <summary>
/// Client-side signatures for known CS cheat loaders / install trees.
/// Hits are reported to the API (ban path). <see cref="Remediate"/> only
/// kills matching processes and deletes paths under known cheat roots —
/// never random user/system files.
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
            // ExLoader
            ["exloader"] = "exloader",
            ["exloaderui"] = "exloader",
            ["ex-loader"] = "exloader",
            // Neverlose
            ["neverlose"] = "neverlose",
            ["neverlose_loader"] = "neverlose",
            ["nl_loader"] = "neverlose",
            // Fatality
            ["fatality"] = "fatality",
            ["fatality_loader"] = "fatality",
            // Nixware
            ["nixware"] = "nixware",
            ["nixware_loader"] = "nixware",
            // Primordial
            ["primordial"] = "primordial",
            ["primordial_loader"] = "primordial",
            // Gamesense / skeet family
            ["gamesense"] = "gamesense",
            ["skeet"] = "gamesense",
            // Onetap
            ["onetap"] = "onetap",
            ["onetapv4"] = "onetap",
            ["otc3"] = "onetap",
            // Aimjunkies / AJ
            ["aimjunkies"] = "aimjunkies",
            ["ajloader"] = "aimjunkies",
            // Spirt / spiral
            ["spirt"] = "spirt",
            ["spirthack"] = "spirt",
            // Interium
            ["interium"] = "interium",
            ["interiumloader"] = "interium",
            // Midnight
            ["midnight"] = "midnight",
            ["midnight_loader"] = "midnight",
            // Nemesis
            ["nemesis"] = "nemesis",
            ["nemesisloader"] = "nemesis",
            // Rawetrip
            ["rawetrip"] = "rawetrip",
            // Osiris (known cheat build names)
            ["osiris"] = "osiris",
            // Cheat Engine — exact image names only (no substring match)
            ["cheatengine-x86_64"] = "cheatengine",
            ["cheatengine-i386"] = "cheatengine",
            ["cheatengine"] = "cheatengine",
        };

    /// <summary>
    /// Install-tree markers: folder name under common roots → signature.
    /// Folder existence alone is enough (same model as ExLoader today).
    /// </summary>
    private static readonly (string Folder, string Signature)[] InstallFolderNames =
    [
        ("ExLoader", "exloader"),
        ("Neverlose", "neverlose"),
        ("neverlose", "neverlose"),
        ("Fatality", "fatality"),
        ("fatality", "fatality"),
        ("Nixware", "nixware"),
        ("nixware", "nixware"),
        ("Primordial", "primordial"),
        ("primordial", "primordial"),
        ("gamesense", "gamesense"),
        ("Gamesense", "gamesense"),
        ("skeet", "gamesense"),
        ("OneTap", "onetap"),
        ("onetap", "onetap"),
        ("Onetap", "onetap"),
        ("Aimjunkies", "aimjunkies"),
        ("aimjunkies", "aimjunkies"),
        ("Spirt", "spirt"),
        ("Interium", "interium"),
        ("Midnight", "midnight"),
        ("NemesisCS", "nemesis"),
        ("Nemesis", "nemesis"),
        ("Rawetrip", "rawetrip"),
        ("Osiris", "osiris"),
    ];

    /// <summary>
    /// Filenames that only count when found *inside* a known cheat install root
    /// (never as a bare match under Program Files / System32).
    /// </summary>
    private static readonly string[] CheatFilesInsideRoots =
    [
        "ExLoader.exe",
        "exloader.exe",
        "ExLoader.dll",
        "neverlose.exe",
        "neverlose_loader.exe",
        "fatality.exe",
        "fatality_loader.exe",
        "nixware.exe",
        "primordial.exe",
        "gamesense.exe",
        "onetap.exe",
        "injector.exe",
        "loader.exe",
        "cheat.dll",
        "hack.dll",
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

    /// <summary>
    /// Kill matching cheat processes, then delete only under known cheat roots.
    /// </summary>
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

        // Always try to wipe full known install trees when any hit for that sig.
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

            foreach (var (folder, signature) in InstallFolderNames)
            {
                string full;
                try { full = Path.Combine(root, folder); }
                catch { continue; }

                try
                {
                    if (!Directory.Exists(full)) continue;

                    add(new Hit(signature, full, null));

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

                    // Cap enumeration — huge trees must not hang the UI thread.
                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(full, "*.exe", SearchOption.AllDirectories)
                                     .Take(30))
                            add(new Hit(signature, f, null));
                    }
                    catch { /* partial ACL */ }
                }
                catch { /* ignore */ }
            }
        }
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

                // If we got a path, only keep it when it sits under a cheat root
                // or the filename itself is a known cheat binary name.
                if (!string.IsNullOrWhiteSpace(path))
                {
                    if (!IsUnderAllowedRoot(path, allowedRoots) &&
                        !IsKnownCheatFileName(path))
                    {
                        // Still report the running process (ban signal) but
                        // leave Path null so Remediate will not delete it.
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

        foreach (var p in procs)
        {
            try
            {
                var name = (p.ProcessName ?? "").Trim();
                if (name.Length == 0) continue;
                if (!ExactProcesses.ContainsKey(name)) continue;

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
            foreach (var (folder, _) in InstallFolderNames)
            {
                try
                {
                    var full = Path.Combine(root, folder);
                    if (Directory.Exists(full))
                        set.Add(Path.GetFullPath(full));
                }
                catch { /* ignore */ }
            }
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

        AddSpecial(Environment.SpecialFolder.LocalApplicationData);
        AddSpecial(Environment.SpecialFolder.ApplicationData);
        AddSpecial(Environment.SpecialFolder.CommonApplicationData);
        AddSpecial(Environment.SpecialFolder.UserProfile);
        AddSpecial(Environment.SpecialFolder.DesktopDirectory);
        AddSpecial(Environment.SpecialFolder.MyDocuments);

        // One level of Desktop/Documents subfolders is enough — deep walks hang.
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
