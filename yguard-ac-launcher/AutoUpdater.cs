using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace YGuardAC;

internal sealed class LauncherRelease
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("download_url")] public string DownloadUrl { get; set; } = "";
    [JsonPropertyName("mandatory")] public bool Mandatory { get; set; }
}

internal static class AutoUpdater
{
    private static readonly string SkipPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YGuardAC", "skipped-update.txt");

    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>
    /// Real install folder of the .exe — NOT AppContext.BaseDirectory (single-file
    /// apps extract to a temp .net cache and BaseDirectory points there).
    /// </summary>
    public static string InstallDirectory
    {
        get
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                var dir = Path.GetDirectoryName(exe);
                if (!string.IsNullOrWhiteSpace(dir)) return dir;
            }
            return AppContext.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    public static string ExePath =>
        Environment.ProcessPath
        ?? Path.Combine(InstallDirectory, "YGuardAC.exe");

    public static async Task<LauncherRelease?> FetchAsync(string apiBase, CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            return await http.GetFromJsonAsync<LauncherRelease>(
                $"{apiBase.TrimEnd('/')}/plugins/ac/launcher", ct);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsNewer(string remoteVersion, string localVersion)
    {
        if (!Version.TryParse(Normalize(remoteVersion), out var remote)) return false;
        if (!Version.TryParse(Normalize(localVersion), out var local)) return true;
        return remote > local;
    }

    public static bool WasSkipped(string remoteVersion)
    {
        try
        {
            if (!File.Exists(SkipPath)) return false;
            return string.Equals(
                File.ReadAllText(SkipPath).Trim(),
                Normalize(remoteVersion),
                StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static void RememberSkip(string remoteVersion)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SkipPath)!);
            File.WriteAllText(SkipPath, Normalize(remoteVersion));
        }
        catch { /* ignore */ }
    }

    private static string Normalize(string v)
    {
        v = (v ?? "").Trim().TrimStart('v', 'V').Split('-', '+')[0];
        var parts = v.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (parts.Count < 3) parts.Add("0");
        return string.Join(".", parts.Take(4));
    }

    /// <summary>
    /// Download zip, stage under %TEMP%, write a bat that replaces the real exe after exit, restart.
    /// </summary>
    public static async Task<(bool ok, string error)> ApplyAsync(
        LauncherRelease release,
        IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(release.DownloadUrl))
            return (false, "missing download url");

        var appDir = InstallDirectory;
        var exePath = ExePath;
        var updateRoot = Path.Combine(
            Path.GetTempPath(),
            "YGuardAC-update-" + Guid.NewGuid().ToString("N")[..8]);
        var zipPath = Path.Combine(updateRoot, "YGuardAC.zip");
        var extractDir = Path.Combine(updateRoot, "extract");

        Directory.CreateDirectory(updateRoot);
        progress?.Report("Downloading update…");

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var bytes = await http.GetByteArrayAsync(release.DownloadUrl);
            if (bytes.Length < 1024)
                return (false, "download too small");
            await File.WriteAllBytesAsync(zipPath, bytes);

            progress?.Report("Extracting…");
            Directory.CreateDirectory(extractDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

            var payloadExe = FindPayloadExe(extractDir);
            if (payloadExe == null)
                return (false, "YGuardAC.exe missing in update zip");

            var bat = Path.Combine(updateRoot, "apply.bat");
            var pid = Environment.ProcessId;
            // Escape for batch: paths with spaces need quotes; use short waits + move.
            var batBody = $"""
                @echo off
                setlocal
                :wait
                tasklist /FI "PID eq {pid}" 2>NUL | find "{pid}" >NUL
                if %ERRORLEVEL%==0 (
                  timeout /t 1 /nobreak >NUL
                  goto wait
                )
                timeout /t 1 /nobreak >NUL
                del /f /q "{exePath}" >NUL 2>&1
                copy /y "{payloadExe}" "{exePath}" >NUL
                if not exist "{exePath}" (
                  copy /y "{payloadExe}" "{exePath}" >NUL
                )
                start "" "{exePath}"
                rd /s /q "{updateRoot}"
                """;
            await File.WriteAllTextAsync(bat, batBody);

            progress?.Report("Restarting…");
            var started = Process.Start(new ProcessStartInfo
            {
                FileName = bat,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = updateRoot,
                CreateNoWindow = true,
            });
            if (started == null)
                return (false, "could not start updater script");

            return (true, "");
        }
        catch (Exception ex)
        {
            try { Directory.Delete(updateRoot, true); } catch { }
            return (false, ex.Message);
        }
    }

    private static string? FindPayloadExe(string extractDir)
    {
        var direct = Path.Combine(extractDir, "YGuardAC.exe");
        if (File.Exists(direct)) return direct;

        foreach (var f in Directory.EnumerateFiles(extractDir, "YGuardAC.exe", SearchOption.AllDirectories))
            return f;
        return null;
    }
}
