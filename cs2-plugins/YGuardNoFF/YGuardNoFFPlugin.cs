using System.Collections.Concurrent;
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
    public override string ModuleVersion => "1.2.2";
    public override string ModuleAuthor => "YGuard";
    public override string ModuleDescription =>
        "Panel-driven FF, bunny hop, and hold-E parachute on public servers";

    /// <summary>Target downward speed while parachuting (units/s).</summary>
    private const float ParachuteFallSpeed = -100f;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };

    private bool _active;
    private bool _friendlyFire;
    private bool _bunnyHop;
    private bool _parachute;
    private string? _baseUrl;
    private string? _serverId;
    private string? _password;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _poll;

    /// <summary>Slot indexes currently under reduced gravity for parachute.</summary>
    private readonly ConcurrentDictionary<int, bool> _paraActive = new();

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
            ClearParachuteGravity();
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
        RegisterEventHandler<EventPlayerDeath>((@event, _) =>
        {
            var player = @event.Userid;
            if (player is not null && player.IsValid)
            {
                StopParachute(player);
            }
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            var player = @event.Userid;
            if (player is not null && player.IsValid)
            {
                _paraActive.TryRemove(player.Slot, out var _removed);
            }
            return HookResult.Continue;
        });

        // Instant panel apply (same pattern as css_yadmin_reload).
        AddCommand(
            "css_yguard_nof_reload",
            "Reload YGuardNoFF gameplay prefs from the panel",
            (player, info) => { _ = SyncAndApplyAsync(); });

        _poll = AddTimer(15f, () => { _ = SyncAndApplyAsync(); },
            CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

        _ = SyncAndApplyAsync();
    }

    public override void Unload(bool hotReload)
    {
        if (_active)
        {
            RemoveListener<Listeners.OnTick>(OnTick);
            ClearParachuteGravity();
        }
        _poll?.Kill();
        _poll = null;
    }

    private void OnTick()
    {
        if (!_active) return;

        foreach (var player in Utilities.GetPlayers())
        {
            if (player is null || !player.IsValid || player.IsBot || !player.PawnIsAlive)
            {
                continue;
            }

            var pawn = player.PlayerPawn.Value;
            if (pawn is null || !pawn.IsValid)
            {
                continue;
            }

            var holdingUse = (player.Buttons & PlayerButtons.Use) != 0;
            var airborne = !pawn.OnGroundLastTick;

            if (_parachute && holdingUse && airborne)
            {
                StartParachute(player, pawn);
            }
            else if (_paraActive.ContainsKey(player.Slot))
            {
                StopParachute(player);
            }
        }
    }

    private void StartParachute(CCSPlayerController player, CCSPlayerPawn pawn)
    {
        if (!_paraActive.ContainsKey(player.Slot))
        {
            _paraActive[player.Slot] = true;
            // GravityScale lives on the pawn (physical body), not the controller.
            pawn.GravityScale = 0.1f;
            player.GravityScale = 0.1f;
        }

        var vel = pawn.AbsVelocity;
        if (vel.Z >= 0f)
        {
            return;
        }

        // Clamp fall speed (same approach as Franc1sco CS2-Parachute).
        if (vel.Z < ParachuteFallSpeed)
        {
            vel.Z = ParachuteFallSpeed;
        }

        var origin = pawn.AbsOrigin;
        var angles = pawn.AbsRotation;
        if (origin is not null && angles is not null)
        {
            pawn.Teleport(origin, angles, vel);
        }
    }

    private void StopParachute(CCSPlayerController player)
    {
        if (!_paraActive.TryRemove(player.Slot, out _))
        {
            return;
        }

        if (!player.IsValid)
        {
            return;
        }

        player.GravityScale = 1.0f;
        var pawn = player.PlayerPawn.Value;
        if (pawn is not null && pawn.IsValid)
        {
            pawn.GravityScale = 1.0f;
        }
    }

    private void ClearParachuteGravity()
    {
        foreach (var slot in _paraActive.Keys.ToArray())
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player is not null && player.IsValid)
            {
                StopParachute(player);
            }
        }
        _paraActive.Clear();
    }

    private async Task SyncAndApplyAsync()
    {
        try
        {
            var prefs = await FetchPrefsAsync();
            if (prefs != null)
            {
                var wasPara = _parachute;
                _friendlyFire = prefs.FriendlyFire;
                _bunnyHop = prefs.BunnyHop;
                _parachute = prefs.Parachute;
                if (wasPara != _parachute)
                {
                    Logger.LogInformation(
                        "YGuardNoFF parachute={Parachute} ff={Ff} bhop={Bhop}",
                        _parachute,
                        _friendlyFire,
                        _bunnyHop);
                    if (!_parachute)
                    {
                        Server.NextFrame(ClearParachuteGravity);
                    }
                }
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

        // Prefer nested gameplay.*; fall back to flat keys for older API builds.
        await using var stream = await resp.Content.ReadAsStreamAsync();
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;
        var src = root.TryGetProperty("gameplay", out var nested) ? nested : root;
        return new GameplayPrefs
        {
            FriendlyFire = ReadBool(src, root, "friendly_fire"),
            BunnyHop = ReadBool(src, root, "bunny_hop"),
            Parachute = ReadBool(src, root, "parachute"),
        };
    }

    private static bool ReadBool(
        System.Text.Json.JsonElement primary,
        System.Text.Json.JsonElement fallback,
        string name)
    {
        if (primary.ValueKind == System.Text.Json.JsonValueKind.Object
            && primary.TryGetProperty(name, out var a)
            && (a.ValueKind == System.Text.Json.JsonValueKind.True
                || a.ValueKind == System.Text.Json.JsonValueKind.False))
        {
            return a.GetBoolean();
        }
        if (fallback.TryGetProperty(name, out var b)
            && (b.ValueKind == System.Text.Json.JsonValueKind.True
                || b.ValueKind == System.Text.Json.JsonValueKind.False))
        {
            return b.GetBoolean();
        }
        return false;
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
