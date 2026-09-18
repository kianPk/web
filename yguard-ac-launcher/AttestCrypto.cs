using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace YGuardAC;

/// <summary>
/// Signs attest payloads with the device token (HMAC-SHA256).
/// Server issues a one-time challenge; replayed / forged JSON is rejected.
/// </summary>
internal static class AttestCrypto
{
    public static string Canonical(Dictionary<string, object?> fields)
    {
        static string Flag(object? v) =>
            v is bool b ? (b ? "1" : "0") : (v is true ? "1" : "0");

        string S(string key)
        {
            var v = fields.GetValueOrDefault(key);
            return v switch
            {
                null => "",
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? "",
                _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "",
            };
        }

        return string.Join(
            "\n",
            $"challenge={S("challenge")}",
            $"ts={S("ts")}",
            $"client_version={S("client_version")}",
            $"hardware_hash={S("hardware_hash")}",
            $"secure_boot={Flag(fields.GetValueOrDefault("secure_boot"))}",
            $"iommu={Flag(fields.GetValueOrDefault("iommu"))}",
            $"tpm_20={Flag(fields.GetValueOrDefault("tpm_20"))}",
            $"tpm_attestation={Flag(fields.GetValueOrDefault("tpm_attestation"))}",
            $"hvci={Flag(fields.GetValueOrDefault("hvci"))}",
            $"windows_updates={Flag(fields.GetValueOrDefault("windows_updates"))}",
            $"cheat_clean={Flag(fields.GetValueOrDefault("cheat_clean"))}");
    }

    public static string Sign(string deviceToken, string canonical)
    {
        var key = Encoding.UTF8.GetBytes(deviceToken);
        var data = Encoding.UTF8.GetBytes(canonical);
        var mac = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(mac).ToLowerInvariant();
    }
}

/// <summary>
/// DPAPI-protect the device token at rest (CurrentUser scope).
/// Plaintext tokens on disk are a common steal-and-replay vector.
/// </summary>
internal static class SecureTokenStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("YGuardAC.v1.device");

    public static void Save(string path, string token)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var plain = Encoding.UTF8.GetBytes(token);
        try
        {
            var protectedBytes = ProtectedData.Protect(
                plain, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }

        var legacy = path + ".txt";
        try { if (File.Exists(legacy)) File.Delete(legacy); } catch { /* ignore */ }
    }

    public static string? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var raw = File.ReadAllBytes(path);
            if (LooksLikePlainToken(raw))
            {
                var plain = Encoding.UTF8.GetString(raw).Trim();
                if (string.IsNullOrEmpty(plain)) return null;
                Save(path, plain);
                return plain;
            }

            var unprotected = ProtectedData.Unprotect(
                raw, Entropy, DataProtectionScope.CurrentUser);
            try
            {
                return Encoding.UTF8.GetString(unprotected).Trim();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(unprotected);
            }
        }
        catch
        {
            return null;
        }
    }

    private static bool LooksLikePlainToken(byte[] raw)
    {
        if (raw.Length < 16 || raw.Length > 128) return false;
        foreach (var b in raw)
        {
            if (b is < 0x20 or > 0x7e) return false;
        }
        return true;
    }
}

/// <summary>Lightweight anti-debug gate before attest.</summary>
internal static class ClientGuard
{
    [DllImport("kernel32.dll")]
    private static extern bool IsDebuggerPresent();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CheckRemoteDebuggerPresent(
        IntPtr hProcess, ref bool isDebuggerPresent);

    public static bool IsTamperedEnvironment()
    {
        try
        {
            if (Debugger.IsAttached) return true;
            if (IsDebuggerPresent()) return true;
            var remote = false;
            CheckRemoteDebuggerPresent(
                System.Diagnostics.Process.GetCurrentProcess().Handle, ref remote);
            if (remote) return true;
        }
        catch { /* fail open */ }

        return false;
    }
}
