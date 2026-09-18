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
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

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

    private static string Normalize(string v)
    {
        v = (v ?? "").Trim().TrimStart('v', 'V').Split('-', '+')[0];
        var parts = v.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (parts.Count < 3) parts.Add("0");
        return string.Join(".", parts.Take(4));
    }

    /// <summary>
    /// Download zip, stage next to the running exe, write a bat that replaces files after exit, then restart.
    /// </summary>
    public static async Task<bool> ApplyAsync(LauncherRelease release, IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(release.DownloadUrl)) return false;

        var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var exePath = Environment.ProcessPath
            ?? Path.Combine(appDir, "YGuardAC.exe");
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
            await File.WriteAllBytesAsync(zipPath, bytes);

            progress?.Report("Extracting…");
            Directory.CreateDirectory(extractDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

            // Zip may be flat (exe + deps) or nested one folder.
            var payload = FindPayloadDir(extractDir);
            if (payload == null || !File.Exists(Path.Combine(payload, "YGuardAC.exe")))
            {
                progress?.Report("Update package invalid");
                return false;
            }

            var bat = Path.Combine(updateRoot, "apply.bat");
            var pid = Environment.ProcessId;
            var exeName = Path.GetFileName(exePath);
            // Wait for exit, replace the exe (single-file) or robocopy folder, restart.
            var batBody = $@"@echo off
setlocal
:wait
tasklist /FI ""PID eq {pid}"" 2>NUL | find ""{pid}"" >NUL
if %ERRORLEVEL%==0 (
  timeout /t 1 /nobreak >NUL
  goto wait
)
copy /Y ""{Path.Combine(payload, "YGuardAC.exe")}"" ""{Path.Combine(appDir, exeName)}"" >NUL
start """" ""{exePath}""
rd /s /q ""{updateRoot}""
";
            await File.WriteAllTextAsync(bat, batBody);

            progress?.Report("Restarting…");
            Process.Start(new ProcessStartInfo
            {
                FileName = bat,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = updateRoot,
            });
            return true;
        }
        catch
        {
            try { Directory.Delete(updateRoot, true); } catch { }
            return false;
        }
    }

    private static string? FindPayloadDir(string extractDir)
    {
        if (File.Exists(Path.Combine(extractDir, "YGuardAC.exe")))
            return extractDir;

        foreach (var dir in Directory.GetDirectories(extractDir))
        {
            if (File.Exists(Path.Combine(dir, "YGuardAC.exe")))
                return dir;
        }
        return null;
    }
}
