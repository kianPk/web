using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace YGuardRanks;

public sealed class RankPlayerDto
{
    [JsonPropertyName("steam_id")]
    public string SteamId { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("points")]
    public int Points { get; set; }

    [JsonPropertyName("kills")]
    public int Kills { get; set; }

    [JsonPropertyName("deaths")]
    public int Deaths { get; set; }

    [JsonPropertyName("assists")]
    public int Assists { get; set; }

    [JsonPropertyName("headshots")]
    public int Headshots { get; set; }

    [JsonPropertyName("skill_group")]
    public int SkillGroup { get; set; }

    [JsonPropertyName("rank_name")]
    public string RankName { get; set; } = "";
}

internal sealed class PanelApi
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    private readonly string? _serverId = Environment.GetEnvironmentVariable("SERVER_ID");
    private readonly string? _password = Environment.GetEnvironmentVariable("SERVER_API_PASSWORD");
    private readonly string? _baseUrl;

    public PanelApi()
    {
        var api = Environment.GetEnvironmentVariable("API_DOMAIN");
        if (!string.IsNullOrWhiteSpace(api))
        {
            _baseUrl = api.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? api.TrimEnd('/')
                : $"https://{api.TrimEnd('/')}";
        }
    }

    public bool Configured =>
        !string.IsNullOrWhiteSpace(_serverId)
        && !string.IsNullOrWhiteSpace(_password)
        && _baseUrl != null;

    public async Task<List<RankPlayerDto>> GetPlayersAsync(IEnumerable<ulong> steamIds)
    {
        if (!Configured) return [];
        var ids = string.Join(",", steamIds.Where(id => id != 0));
        if (string.IsNullOrEmpty(ids)) return [];
        using var req = Request(
            HttpMethod.Get,
            $"/hosted-servers/plugin/ranks/players?server_id={Uri.EscapeDataString(_serverId!)}&steam_ids={Uri.EscapeDataString(ids)}");
        using var resp = await Http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            Console.WriteLine($"[YGuardRanks] players HTTP {(int)resp.StatusCode}");
            return [];
        }
        var body = await resp.Content.ReadFromJsonAsync<PlayersEnvelope>();
        return body?.Players ?? [];
    }

    public async Task<List<RankPlayerDto>> GetTopAsync(int limit = 10)
    {
        if (!Configured) return [];
        using var req = Request(
            HttpMethod.Get,
            $"/hosted-servers/plugin/ranks/top?server_id={Uri.EscapeDataString(_serverId!)}&limit={limit}");
        using var resp = await Http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            Console.WriteLine($"[YGuardRanks] top HTTP {(int)resp.StatusCode}");
            return [];
        }
        var body = await resp.Content.ReadFromJsonAsync<PlayersEnvelope>();
        return body?.Players ?? [];
    }

    public async Task<List<RankPlayerDto>> SyncAsync(IEnumerable<object> events)
    {
        if (!Configured) return [];
        using var req = Request(HttpMethod.Post, "/hosted-servers/plugin/ranks/sync");
        req.Content = JsonContent.Create(new { server_id = _serverId, events });
        using var resp = await Http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            Console.WriteLine($"[YGuardRanks] sync HTTP {(int)resp.StatusCode}");
            return [];
        }
        var body = await resp.Content.ReadFromJsonAsync<PlayersEnvelope>();
        return body?.Players ?? [];
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, $"{_baseUrl}{path}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _password);
        return req;
    }

    private sealed class PlayersEnvelope
    {
        [JsonPropertyName("players")]
        public List<RankPlayerDto> Players { get; set; } = [];
    }
}
