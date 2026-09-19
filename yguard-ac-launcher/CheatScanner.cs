using System.Diagnostics;

namespace YGuardAC;

/// <summary>
/// Client-side signatures for known CS cheat loaders/install paths.
/// Presence is reported to the API for ban; <see cref="Remediate"/> also
/// kills processes and deletes the install tree when possible.
/// </summary>
internal static class CheatScanner
{
    public sealed record Hit(string Signature, string? Path, string? ProcessName);

    private static readonly string[] ExLoaderDirs =
    [
        @"C:\Program Files\ExLoader",
        @"C:\Program Files (x86)\ExLoader",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExLoader"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ExLoader"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ExLoader"),
    ];

    private static readonly string[] ExLoaderFiles =
    [
        "ExLoader.exe",
        "exloader.exe",
        "ExLoader.dll",
        "loader.exe",
        "injector.exe",
    ];

    private static readonly string[] ProcessNames =
    [
        "exloader",
        "exloaderui",
        "ex-loader",
    ];

    public static List<Hit> Scan()
    {
        var hits = new List<Hit>();
        ScanExLoader(hits);
        ScanProcesses(hits);
        return hits;
    }

    /// <summary>
    /// Kill matching cheat processes, then delete files/folders from hits.
    /// Returns how many paths were successfully removed.
    /// </summary>
    public static int Remediate(IReadOnlyList<Hit> hits)
    {
        if (hits.Count == 0) return 0;

        KillMatchingProcesses();

        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hit in hits)
        {
            if (string.IsNullOrWhiteSpace(hit.Path)) continue;
            try
            {
                if (File.Exists(hit.Path))
                    files.Add(hit.Path);
                else if (Directory.Exists(hit.Path))
                    dirs.Add(hit.Path);
            }
            catch { /* ignore */ }
        }

        foreach (var dir in ExLoaderDirs)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    dirs.Add(dir);
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
            catch { /* locked / access denied */ }
        }

        // Longest paths first so nested dirs clear before parents.
        foreach (var dir in dirs.OrderByDescending(d => d.Length))
        {
            try
            {
                if (!Directory.Exists(dir)) continue;
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
            catch { /* access denied — Program Files may need elevation */ }
        }

        return removed;
    }

    private static void KillMatchingProcesses()
    {
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var name = (p.ProcessName ?? "").Trim().ToLowerInvariant();
                    if (name.Length == 0) continue;
                    if (!ProcessNames.Any(n => name == n || name.Contains(n)))
                        continue;
                    try { p.Kill(entireProcessTree: true); } catch { try { p.Kill(); } catch { } }
                    try { p.WaitForExit(2000); } catch { }
                }
                catch { }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }
        }
        catch { }
    }

    private static void ScanExLoader(List<Hit> hits)
    {
        foreach (var dir in ExLoaderDirs)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                    continue;

                // Folder itself is enough — user installs ExLoader here.
                hits.Add(new Hit("exloader", dir, null));

                foreach (var name in ExLoaderFiles)
                {
                    var full = Path.Combine(dir, name);
                    if (File.Exists(full))
                        hits.Add(new Hit("exloader", full, null));
                }

                // Any exe/dll under the install tree
                try
                {
                    foreach (var f in Directory.EnumerateFiles(dir, "*.exe", SearchOption.AllDirectories)
                                 .Take(20))
                        hits.Add(new Hit("exloader", f, null));
                    foreach (var f in Directory.EnumerateFiles(dir, "*.dll", SearchOption.TopDirectoryOnly)
                                 .Take(10))
                        hits.Add(new Hit("exloader", f, null));
                }
                catch { /* access denied on some children — folder hit already recorded */ }
            }
            catch { /* ignore */ }
        }
    }

    private static void ScanProcesses(List<Hit> hits)
    {
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var name = (p.ProcessName ?? "").Trim().ToLowerInvariant();
                    if (name.Length == 0) continue;
                    if (!ProcessNames.Any(n => name == n || name.Contains(n)))
                        continue;

                    string? path = null;
                    try { path = p.MainModule?.FileName; } catch { /* access denied */ }
                    hits.Add(new Hit("exloader", path, p.ProcessName));
                }
                catch { }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }
        }
        catch { }
    }
}
