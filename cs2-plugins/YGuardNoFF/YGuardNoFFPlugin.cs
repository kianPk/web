using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace YGuardNoFF;

/// <summary>
/// Panel gameplay prefs on Public/Custom dedicated servers:
/// friendly fire, auto bunny hop, and hold-E parachute (slow fall).
/// Ranked match pods and Practice are left alone.
/// </summary>
public class YGuardNoFFPlugin : BasePlugin
{
    public override string ModuleName => "YGuard No Friendly Fire";
    public override string ModuleVersion => "1.2.0";
    public override string ModuleAuthor => "YGuard";
    public override string ModuleDescription =>
        "Panel-driven FF, bunny hop, and hold-E parachute on public servers";

    /// <summary>Max downward speed while parachuting (units/s). Soft float.</summary>
    private const float ParachuteMaxFallSpeed = -140f;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };

    private bool _active;
    private bool _friendlyFire;
    private bool _bunnyHop;
    private bool _parachute;
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
        RegisterListener<Listeners.OnTick>(OnTick);
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
        if (_active)
        {
            RemoveListener<Listeners.OnTick>(OnTick);
        }
        _poll?.Kill();
        _poll = null;
    }

    private void OnTick()
    {
        if (!_active || !_parachute) return;

        foreach (var player in Utilities.GetPlayers())
        {
            if (player is null || !player.IsValid || player.IsBot || !player.PawnIsAlive)
            {
                continue;
            }

            // E / +use
            if ((player.Buttons & PlayerButtons.Use) == 0)
            {
                continue;
            }

            var pawn = player.PlayerPawn.Value;
            if (pawn is null || !pawn.IsValid)
            {
                continue;
            }

            if ((pawn.Flags & (uint)PlayerFlags.FL_ONGROUND) != 0)
            {
                continue;
            }

            var vel = pawn.AbsVelocity;
            // Only dampen a real fall; leave upward / float alone.
            if (vel.Z >= -40f || vel.Z >= ParachuteMaxFallSpeed)
            {
                continue;
            }

            pawn.Teleport(null, null, new Vector(vel.X, vel.Y, ParachuteMaxFallSpeed));
        }
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
                _parachute = prefs.Parachute;
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

        [JsonPropertyName("parachute")]
        public bool Parachute { get; set; }
    }
}
