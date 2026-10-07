using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Logging;

namespace YGuardNoFF;

/// <summary>
/// Applies panel gameplay prefs (friendly fire + bunny hop) on Public/Custom
/// dedicated servers. Defaults to FF off / bhop off when the panel is unreachable.
/// Ranked match pods and Practice are left alone.
/// </summary>
public class YGuardNoFFPlugin : BasePlugin
{
    public override string ModuleName => "YGuard No Friendly Fire";
    public override string ModuleVersion => "1.1.0";
    public override string ModuleAuthor => "YGuard";
    public override string ModuleDescription =>
        "Panel-driven friendly fire and bunny hop on non-Ranked dedicated servers";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };

    private bool _active;
    private bool _friendlyFire;
    private bool _bunnyHop;
    private string? _baseUrl;
    private string? _serverId;
    private string? _password;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _poll;

    public override void Load(bool hotReload)
    {
        var serverType = (Environment.GetEnvironmentVariable("SERVER_TYPE") ?? "").Trim();
        _active =
            !string.Equals(serverType, "Ranked", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(serverType, "Practice", StringComparison.OrdinalIgnoreCase);

        if (!_active)
        {
            Logger.LogInformation(
                "YGuardNoFF idle (SERVER_TYPE={Type})",
                string.IsNullOrEmpty(serverType) ? "(unset)" : serverType);
            return;
        }

        var api = Environment.GetEnvironmentVariable("API_DOMAIN");
        if (!string.IsNullOrWhiteSpace(api))
        {
            _baseUrl = api.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? api.TrimEnd('/')
                : $"https://{api.TrimEnd('/')}";
        }
        _serverId = Environment.GetEnvironmentVariable("SERVER_ID");
        _password = Environment.GetEnvironmentVariable("SERVER_API_PASSWORD");

        Logger.LogInformation(
            "YGuardNoFF active — syncing gameplay prefs (SERVER_TYPE={Type})",
            serverType);

        RegisterListener<Listeners.OnMapStart>(_map =>
        {
            _ = SyncAndApplyAsync();
        });
        RegisterEventHandler<EventRoundStart>((_, _) =>
        {
            ApplyLocal();
            return HookResult.Continue;
        });
        RegisterEventHandler<EventWarmupEnd>((_, _) =>
        {
            ApplyLocal();
            return HookResult.Continue;
        });

        _poll = AddTimer(30f, () => { _ = SyncAndApplyAsync(); },
            CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

        _ = SyncAndApplyAsync();
    }

    public override void Unload(bool hotReload)
    {
        _poll?.Kill();
        _poll = null;
    }

    private async Task SyncAndApplyAsync()
    {
        try
        {
            var prefs = await FetchPrefsAsync();
            if (prefs != null)
            {
                _friendlyFire = prefs.FriendlyFire;
                _bunnyHop = prefs.BunnyHop;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "YGuardNoFF panel sync failed — keeping last prefs");
        }

        Server.NextFrame(ApplyLocal);
    }

    private async Task<GameplayPrefs?> FetchPrefsAsync()
    {
        if (string.IsNullOrWhiteSpace(_baseUrl)
            || string.IsNullOrWhiteSpace(_serverId)
            || string.IsNullOrWhiteSpace(_password))
        {
            return null;
        }

        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_baseUrl}/hosted-servers/plugin/state?server_id={Uri.EscapeDataString(_serverId)}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _password);
        using var resp = await Http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            Logger.LogWarning("YGuardNoFF plugin/state HTTP {Status}", (int)resp.StatusCode);
            return null;
        }

        return await resp.Content.ReadFromJsonAsync<GameplayPrefs>();
    }

    private void ApplyLocal()
    {
        if (!_active) return;
        Server.NextFrame(() =>
        {
            if (_friendlyFire)
            {
                Server.ExecuteCommand("mp_friendlyfire 1");
                Server.ExecuteCommand("mp_tkpunish 0");
                Server.ExecuteCommand("ff_damage_reduction_bullets 0.33");
                Server.ExecuteCommand("ff_damage_reduction_grenade 0.85");
                Server.ExecuteCommand("ff_damage_reduction_grenade_self 1");
                Server.ExecuteCommand("ff_damage_reduction_other 0.4");
            }
            else
            {
                Server.ExecuteCommand("mp_friendlyfire 0");
                Server.ExecuteCommand("mp_tkpunish 0");
                Server.ExecuteCommand("ff_damage_reduction_bullets 0");
                Server.ExecuteCommand("ff_damage_reduction_grenade 0");
                Server.ExecuteCommand("ff_damage_reduction_grenade_self 0");
                Server.ExecuteCommand("ff_damage_reduction_other 0");
            }

            Server.ExecuteCommand($"sv_autobunnyhopping {(_bunnyHop ? "1" : "0")}");
            Server.ExecuteCommand($"sv_enablebunnyhopping {(_bunnyHop ? "1" : "0")}");
        });
    }

    private sealed class GameplayPrefs
    {
        [JsonPropertyName("friendly_fire")]
        public bool FriendlyFire { get; set; }

        [JsonPropertyName("bunny_hop")]
        public bool BunnyHop { get; set; }
    }
}
