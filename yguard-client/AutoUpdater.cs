using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace YGuardClient;

internal sealed class ClientRelease
{
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("download_url")] public string DownloadUrl { get; set; } = "";
    [JsonPropertyName("mandatory")] public bool Mandatory { get; set; }
    [JsonPropertyName("min_version")] public string? MinVersion { get; set; }
}

internal static class AutoUpdater
{
    private static readonly string SkipPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YGuardClient", "skipped-update.txt");

    private static readonly string PrefPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YGuardClient", "auto-update.txt");

    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

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
        ?? Path.Combine(InstallDirectory, "YGuardClient.exe");

    public static bool IsAutoUpdateEnabled()
    {
        try
        {
            if (!File.Exists(PrefPath)) return true;
            var raw = File.ReadAllText(PrefPath).Trim();
            return raw is not ("0" or "false" or "off" or "no");
        }
        catch
        {
            return true;
        }
    }

    public static void SetAutoUpdateEnabled(bool enabled)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefPath)!);
            File.WriteAllText(PrefPath, enabled ? "1" : "0");
        }
        catch { /* ignore */ }
    }

    public static async Task<ClientRelease?> FetchAsync(CancellationToken ct = default)
    {
        var urls = new[]
        {
            "https://api.yguard.ir/plugins/client/launcher",
            "https://yguard.ir/downloads/YGuardClient.manifest.json",
        };
        foreach (var url in urls)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
                var release = await http.GetFromJsonAsync<ClientRelease>(url, ct);
                if (release != null && !string.IsNullOrWhiteSpace(release.Version))
                    return release;
            }
            catch
            {
                /* try next */
            }
        }
        return null;
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

    public static string Normalize(string v)
    {
        v = (v ?? "").Trim().TrimStart('v', 'V').Split('-', '+')[0];
        var parts = v.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (parts.Count < 3) parts.Add("0");
        return string.Join(".", parts.Take(4));
    }

    public static async Task<(bool ok, string error)> ApplyAsync(
        ClientRelease release,
        IProgress<string>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(release.DownloadUrl))
            return (false, "missing download url");

        var appDir = InstallDirectory;
        var exePath = ExePath;
        var updateRoot = Path.Combine(
            Path.GetTempPath(),
            "YGuardClient-update-" + Guid.NewGuid().ToString("N")[..8]);
        var zipPath = Path.Combine(updateRoot, "YGuardClient.zip");
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
                return (false, "YGuardClient.exe missing in update zip");

            var payloadVer = ReadFileVersion(payloadExe);
            if (!string.IsNullOrEmpty(payloadVer) &&
                !IsNewer(payloadVer, CurrentVersion) &&
                !string.Equals(Normalize(payloadVer), Normalize(CurrentVersion),
                    StringComparison.OrdinalIgnoreCase))
            {
                return (false,
                    $"update zip ({payloadVer}) is not newer than running {CurrentVersion}");
            }

            // Also copy sidecars (steam_api64.dll / steam_appid.txt) if present.
            var bat = Path.Combine(updateRoot, "apply.bat");
            var pid = Environment.ProcessId;
            var payloadDir = Path.GetDirectoryName(payloadExe)!;
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
                if exist "{payloadDir}\steam_api64.dll" copy /y "{payloadDir}\steam_api64.dll" "{appDir}\steam_api64.dll" >NUL
                if exist "{payloadDir}\steam_appid.txt" copy /y "{payloadDir}\steam_appid.txt" "{appDir}\steam_appid.txt" >NUL
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
        var direct = Path.Combine(extractDir, "YGuardClient.exe");
        if (File.Exists(direct)) return direct;
        foreach (var f in Directory.EnumerateFiles(extractDir, "YGuardClient.exe", SearchOption.AllDirectories))
            return f;
        return null;
    }

    private static string ReadFileVersion(string exePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            var v = info.FileVersion ?? info.ProductVersion ?? "";
            return Normalize(v.Split('+')[0]);
        }
        catch
        {
            return "";
        }
    }
}
