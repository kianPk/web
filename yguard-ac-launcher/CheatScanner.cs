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
            ["undetek-v10.47"] = "undetek",
            ["undetek"] = "undetek",
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
        "undetek-v10.47.exe",
        "undetek.exe",
    ];

    /// <summary>
    /// Exact / glob filenames that are a hit anywhere on fixed disks
    /// (not only under known cheat install roots).
    /// </summary>
    private static readonly string[] GlobalBannedFileGlobs =
    [
        "undetek-v10.47.exe",
        "undetek*.exe",
    ];

    private static readonly string[] SearchRoots = BuildSearchRoots();

    private static readonly object GlobalScanGate = new();
    private static List<Hit> GlobalBannedCache = new();
    private static DateTime GlobalScanStartedUtc = DateTime.MinValue;
    private static DateTime GlobalScanFinishedUtc = DateTime.MinValue;
    private static Task? GlobalScanTask;
    private static readonly TimeSpan GlobalScanInterval = TimeSpan.FromMinutes(5);

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
        try { ScanGlobalBannedFiles(Add); } catch { /* ignore */ }

        return hits;
    }

    /// <summary>
    /// Kick a full-disk banned-file walk early (e.g. on launcher start) so the
    /// first attest is less likely to race an empty cache.
    /// </summary>
    public static void WarmGlobalScan()
    {
        try { EnsureGlobalBannedScan(force: false); } catch { /* ignore */ }
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
            // Global banned filenames (undetek-*.exe, …) may sit anywhere —
            // still safe to delete that exact file, not arbitrary trees.
            var allowAnywhere = IsGlobalBannedFileName(hit.Path);
            if (!allowAnywhere && !IsUnderAllowedRoot(hit.Path, allowedRoots))
                continue;
            try
            {
                if (File.Exists(hit.Path))
                    files.Add(hit.Path);
                else if (!allowAnywhere && Directory.Exists(hit.Path))
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
        if (CheatFilesInsideRoots.Any(f =>
                f.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return true;
        return IsGlobalBannedFileName(path);
    }

    private static bool IsGlobalBannedFileName(string path)
    {
        string name;
        try { name = Path.GetFileName(path); }
        catch { return false; }
        if (string.IsNullOrWhiteSpace(name)) return false;

        foreach (var glob in GlobalBannedFileGlobs)
        {
            if (glob.Contains('*') || glob.Contains('?'))
            {
                if (MatchesSimpleGlob(name, glob))
                    return true;
            }
            else if (name.Equals(glob, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool MatchesSimpleGlob(string name, string glob)
    {
        // Only supports a single '*' (e.g. undetek*.exe).
        var star = glob.IndexOf('*');
        if (star < 0)
            return name.Equals(glob, StringComparison.OrdinalIgnoreCase);
        var prefix = glob[..star];
        var suffix = glob[(star + 1)..];
        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
               && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
               && name.Length >= prefix.Length + suffix.Length;
    }

    private static void ScanGlobalBannedFiles(Action<Hit> add)
    {
        EnsureGlobalBannedScan(force: false);

        List<Hit> snapshot;
        lock (GlobalScanGate)
            snapshot = GlobalBannedCache.ToList();

        foreach (var hit in snapshot)
        {
            if (string.IsNullOrWhiteSpace(hit.Path)) continue;
            try
            {
                if (File.Exists(hit.Path))
                    add(hit);
            }
            catch { /* ignore */ }
        }
    }

    private static void EnsureGlobalBannedScan(bool force)
    {
        lock (GlobalScanGate)
        {
            if (GlobalScanTask is { IsCompleted: false })
                return;

            var now = DateTime.UtcNow;
            if (!force
                && GlobalScanFinishedUtc != DateTime.MinValue
                && now - GlobalScanFinishedUtc < GlobalScanInterval)
                return;

            GlobalScanStartedUtc = now;
            GlobalScanTask = Task.Run(RunGlobalBannedFileScan);
        }
    }

    /// <summary>
    /// Block until the first full-disk banned-file walk finishes (or timeout).
    /// Used before attest so a late hit cannot slip a clean unlock.
    /// </summary>
    public static void WaitForInitialGlobalScan(TimeSpan timeout)
    {
        EnsureGlobalBannedScan(force: false);
        Task? task;
        lock (GlobalScanGate)
        {
            if (GlobalScanFinishedUtc != DateTime.MinValue)
                return;
            task = GlobalScanTask;
        }
        if (task == null) return;
        try { task.Wait(timeout); } catch { /* ignore */ }
    }

    private static void RunGlobalBannedFileScan()
    {
        var found = new List<Hit>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            DriveInfo[] drives;
            try { drives = DriveInfo.GetDrives(); }
            catch { drives = Array.Empty<DriveInfo>(); }

            foreach (var drive in drives)
            {
                try
                {
                    if (!drive.IsReady) continue;
                    if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                        continue;
                    WalkForBannedFiles(drive.RootDirectory.FullName, found, seen);
                }
                catch { /* ignore drive */ }
            }
        }
        catch { /* never throw */ }

        lock (GlobalScanGate)
        {
            GlobalBannedCache = found;
            GlobalScanFinishedUtc = DateTime.UtcNow;
        }
    }

    private static readonly HashSet<string> SkipDirNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "$Recycle.Bin",
            "System Volume Information",
            "Windows",
            "WinSxS",
            "Windows.old",
            "Recovery",
            "PerfLogs",
            "node_modules",
            ".git",
            "CSC", // offline files cache
        };

    private static void WalkForBannedFiles(
        string root,
        List<Hit> found,
        HashSet<string> seen)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            try
            {
                var di = new DirectoryInfo(dir);
                if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                foreach (var glob in GlobalBannedFileGlobs)
                {
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(dir, glob))
                        {
                            if (!seen.Add(file)) continue;
                            found.Add(new Hit("undetek", file, null));
                        }
                    }
                    catch { /* ACL */ }
                }

                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    try
                    {
                        var name = Path.GetFileName(sub);
                        if (string.IsNullOrEmpty(name)) continue;
                        if (SkipDirNames.Contains(name)) continue;
                        // Still walk Users / Program Files — only skip heavy OS trees.
                        if (name.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                            continue;
                        var subInfo = new DirectoryInfo(sub);
                        if ((subInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                            continue;
                        stack.Push(sub);
                    }
                    catch { /* ignore */ }
                }
            }
            catch { /* access denied */ }
        }
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
