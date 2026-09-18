using System.Diagnostics;

namespace YGuardAC;

/// <summary>
/// Client-side signatures for known CS cheat loaders/install paths.
/// Presence of these files/folders or running processes is reported to the API for ban.
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
