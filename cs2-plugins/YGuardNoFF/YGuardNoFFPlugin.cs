using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace YGuardNoFF;

/// <summary>
/// Panel gameplay prefs on Public/Custom dedicated servers:
/// friendly fire, auto bunny hop, and hold-E parachute (slow fall).
/// Parachute physics follow Franc1sco/CS2-Parachute (GravityScale + fall clamp).
/// Ranked match pods and Practice are left alone.
/// </summary>
public class YGuardNoFFPlugin : BasePlugin
{
    public override string ModuleName => "YGuard No Friendly Fire";
    public override string ModuleVersion => "1.2.3";
    public override string ModuleAuthor => "YGuard";
    public override string ModuleDescription =>
        "Panel-driven FF, bunny hop, and hold-E parachute on public servers";

    // Franc1sco defaults: FallSpeed 100, Linear true, DecreaseVec 50.
    private const float FallSpeed = 100f;
    private const float DecreaseVec = 50f;
    private const bool Linear = true;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };

    private bool _active;
    private bool _friendlyFire;
    private bool _bunnyHop;
    private bool _parachute;
    private string? _baseUrl;
    private string? _serverId;
    private string? _password;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _poll;

    // Same bookkeeping as Franc1sco CS2-Parachute (keyed by player.Index).
    private readonly Dictionary<int, bool> _usingPara = new();
    private readonly Dictionary<int, int> _paraTicks = new();

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

        if (hotReload)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                EnsureParaState(player);
            }
        }

        RegisterListener<Listeners.OnMapStart>(mapName =>
        {
            StopAllParachutes();
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
        RegisterEventHandler<EventPlayerConnectFull>((@event, info) =>
        {
            EnsureParaState(@event.Userid);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerDisconnect>((@event, info) =>
        {
            var player = @event.Userid;
            if (player is null || !player.IsValid)
            {
                return HookResult.Continue;
            }

            var index = (int)player.Index;
            _usingPara.Remove(index);
            _paraTicks.Remove(index);
            return HookResult.Continue;
        });
        RegisterEventHandler<EventPlayerDeath>((@event, info) =>
        {
            var player = @event.Userid;
            if (player is not null
                && player.IsValid
                && _usingPara.TryGetValue((int)player.Index, out var usingPara)
                && usingPara)
            {
                _usingPara[(int)player.Index] = false;
                StopPara(player);
            }
            return HookResult.Continue;
        });

        AddCommand(
            "css_yguard_nof_reload",
            "Reload YGuardNoFF gameplay prefs from the panel",
            (_, _) => { _ = SyncAndApplyAsync(); });

        // Manual override / debug: css_yguard_parachute 1|0
        AddCommand(
            "css_yguard_parachute",
            "Force parachute on/off (overrides until next panel sync)",
            (player, info) =>
            {
                var arg = info.ArgByIndex(1);
                if (string.Equals(arg, "1", StringComparison.Ordinal)
                    || string.Equals(arg, "on", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "true", StringComparison.OrdinalIgnoreCase))
                {
                    _parachute = true;
                    Logger.LogInformation("YGuardNoFF parachute forced ON");
                }
                else if (string.Equals(arg, "0", StringComparison.Ordinal)
                    || string.Equals(arg, "off", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "false", StringComparison.OrdinalIgnoreCase))
                {
                    _parachute = false;
                    Server.NextFrame(StopAllParachutes);
                    Logger.LogInformation("YGuardNoFF parachute forced OFF");
                }
                else
                {
                    Logger.LogInformation(
                        "YGuardNoFF parachute={Parachute} ff={Ff} bhop={Bhop}",
                        _parachute,
                        _friendlyFire,
                        _bunnyHop);
                }
            });

        _poll = AddTimer(15f, () => { _ = SyncAndApplyAsync(); },
            CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

        _ = SyncAndApplyAsync();
    }

    public override void Unload(bool hotReload)
    {
        if (_active)
        {
            RemoveListener<Listeners.OnTick>(OnTick);
            StopAllParachutes();
        }
        _poll?.Kill();
        _poll = null;
    }

    private void EnsureParaState(CCSPlayerController? player)
    {
        if (player is null || !player.IsValid || player.IsBot)
        {
            return;
        }

        var index = (int)player.Index;
        _usingPara.TryAdd(index, false);
        _paraTicks.TryAdd(index, 0);
    }

    private void OnTick()
    {
        if (!_active)
        {
            return;
        }

        foreach (var player in Utilities.GetPlayers())
        {
            if (player is null || !player.IsValid || player.IsBot || !player.PawnIsAlive)
            {
                continue;
            }

            EnsureParaState(player);
            var index = (int)player.Index;
            var pawn = player.PlayerPawn.Value;
            if (pawn is null || !pawn.IsValid)
            {
                continue;
            }

            // Franc1sco: hold Use (E) while airborne.
            var holdingUse = (player.Buttons & PlayerButtons.Use) != 0;
            var airborne = !pawn.OnGroundLastTick;

            if (_parachute && holdingUse && airborne)
            {
                StartPara(player);
            }
            else if (_usingPara.TryGetValue(index, out var usingPara) && usingPara)
            {
                _usingPara[index] = false;
                StopPara(player);
            }
        }
    }

    /// <summary>
    /// Port of Franc1sco CS2-Parachute StartPara (model spawning omitted).
    /// </summary>
    private void StartPara(CCSPlayerController player)
    {
        var index = (int)player.Index;
        if (!_usingPara.TryGetValue(index, out var usingPara) || !usingPara)
        {
            _usingPara[index] = true;
            // Franc1sco sets GravityScale on the controller.
            player.GravityScale = 0.1f;
            var pawnBody = player.PlayerPawn.Value;
            if (pawnBody is not null && pawnBody.IsValid)
            {
                pawnBody.GravityScale = 0.1f;
            }
        }

        var fallspeed = FallSpeed * -1.0f;
        var pawn = player.PlayerPawn.Value;
        if (pawn is null || !pawn.IsValid)
        {
            return;
        }

        var velocity = pawn.AbsVelocity;
        var isFallSpeed = velocity.Z >= fallspeed;

        if (velocity.Z >= 0.0f)
        {
            return;
        }

        // Franc1sco: (isFallSpeed && Linear) || DecreaseVec == 0
        if ((isFallSpeed && Linear) || DecreaseVec == 0.0f)
        {
            velocity.Z = fallspeed;
        }
        else
        {
            velocity.Z += DecreaseVec;
        }

        var position = pawn.AbsOrigin;
        var angle = pawn.AbsRotation;
        if (position is null || angle is null)
        {
            return;
        }

        // Pawn teleport every tick (controller.Teleport is obsolete / non-physical).
        pawn.Teleport(position, angle, velocity);
        _paraTicks[index] = 0;
    }

    private void StopPara(CCSPlayerController player)
    {
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

        var index = (int)player.Index;
        _paraTicks[index] = 0;
    }

    private void StopAllParachutes()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (player is null || !player.IsValid)
            {
                continue;
            }

            var index = (int)player.Index;
            if (_usingPara.TryGetValue(index, out var usingPara) && usingPara)
            {
                _usingPara[index] = false;
                StopPara(player);
            }
        }
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
                        Server.NextFrame(StopAllParachutes);
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
            Logger.LogWarning(
                "YGuardNoFF missing API env (API_DOMAIN/SERVER_ID/SERVER_API_PASSWORD)");
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

        await using var stream = await resp.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;
        var src = root.TryGetProperty("gameplay", out var nested) ? nested : root;
        return new GameplayPrefs
        {
            FriendlyFire = ReadBool(src, root, "friendly_fire"),
            BunnyHop = ReadBool(src, root, "bunny_hop"),
            Parachute = ReadBool(src, root, "parachute"),
        };
    }

    private static bool ReadBool(JsonElement primary, JsonElement fallback, string name)
    {
        if (primary.ValueKind == JsonValueKind.Object
            && primary.TryGetProperty(name, out var a)
            && (a.ValueKind == JsonValueKind.True || a.ValueKind == JsonValueKind.False))
        {
            return a.GetBoolean();
        }

        if (fallback.TryGetProperty(name, out var b)
            && (b.ValueKind == JsonValueKind.True || b.ValueKind == JsonValueKind.False))
        {
            return b.GetBoolean();
        }

        return false;
    }

    private void ApplyLocal()
    {
        if (!_active)
        {
            return;
        }

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
