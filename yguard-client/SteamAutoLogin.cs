using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Steamworks;

namespace YGuardClient;

/// <summary>
/// Uses the locally logged-in Steam account (CS2 app id 730) to get an auth
/// ticket, exchanges it with the API for a one-time token, then loads that
/// token in WebView so the panel session cookie is set without a browser Steam
/// OpenID prompt.
/// </summary>
internal static class SteamAutoLogin
{
    // Must match API STEAM_AUTH_APP_ID (default Spacewar — works with a normal Web API key).
    private static readonly uint AppId = uint.TryParse(
        Environment.GetEnvironmentVariable("STEAM_AUTH_APP_ID"),
        out var id)
        ? id
        : 480u;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    public static string WebBase { get; set; } =
        Environment.GetEnvironmentVariable("YGUARD_WEB_BASE")?.TrimEnd('/')
        ?? "https://yguard.ir";

    public static async Task<string?> TryGetDesktopLoginUrlAsync(
        CancellationToken ct = default)
    {
        if (!TryInitSteam())
            return null;

        AuthTicket? ticket = null;
        try
        {
            ticket = await SteamUser.GetAuthSessionTicketAsync();
            if (ticket?.Data is null || ticket.Data.Length == 0)
                return null;

            var hex = Convert.ToHexString(ticket.Data);
            var res = await Http.PostAsJsonAsync(
                $"{WebBase}/auth/steam/ticket",
                new { ticket = hex },
                ct);
            if (!res.IsSuccessStatusCode)
                return null;

            var body = await res.Content.ReadFromJsonAsync<TicketResponse>(
                cancellationToken: ct);
            if (string.IsNullOrWhiteSpace(body?.Token))
                return null;

            var redirect = Uri.EscapeDataString("/launcher?desktop=1");
            return $"{WebBase}/auth/steam/desktop?token={body.Token}&redirect={redirect}";
        }
        catch
        {
            return null;
        }
        finally
        {
            try { ticket?.Cancel(); } catch { /* ignore */ }
            try { SteamClient.Shutdown(); } catch { /* ignore */ }
        }
    }

    private static bool TryInitSteam()
    {
        try
        {
            // Drop steam_appid.txt so Steamworks binds to CS2 while Steam is running.
            var appIdPath = Path.Combine(AppContext.BaseDirectory, "steam_appid.txt");
            File.WriteAllText(appIdPath, AppId.ToString());

            if (SteamClient.IsValid)
                return true;

            SteamClient.Init(AppId);
            return SteamClient.IsValid && SteamClient.IsLoggedOn;
        }
        catch
        {
            return false;
        }
    }

    private sealed class TicketResponse
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }

        [JsonPropertyName("steam_id")]
        public string? SteamId { get; set; }
    }
}
