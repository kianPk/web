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
/// Parachute: Franc1sco/le1t style — pawn.GravityScale + AbsVelocity clamp.
/// Only Ranked match pods are fully idle.
/// </summary>
public class YGuardNoFFPlugin : BasePlugin
{
    public override string ModuleName => "YGuard No Friendly Fire";
    public override string ModuleVersion => "1.2.4";
    public override string ModuleAuthor => "YGuard";
    public override string ModuleDescription =>
        "Panel-driven FF, bunny hop, and hold-E parachute on public servers";

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

    private readonly Dictionary<int, bool> _usingPara = new();
    private readonly Dictionary<int, int> _paraTicks = new();

    public override void Load(bool hotReload)
    {
        var serverType = (Environment.GetEnvironmentVariable("SERVER_TYPE") ?? "").Trim();
        // Only Ranked match pods stay fully idle. Hosted Competitive/Casual/Practice
        // must run so panel gameplay (incl. parachute) works.
        _active = !string.Equals(serverType, "Ranked", StringComparison.OrdinalIgnoreCase);

        // Always register the debug command so RCON can prove the DLL is loaded
        // even if we later decide the pod type should idle.
        AddCommand(
            "css_yguard_parachute",
            "Force parachute on/off or print status",
            OnParachuteCommand);
        AddCommand(
            "css_yguard_nof_reload",
            "Reload YGuardNoFF gameplay prefs from the panel",
            (_, _) =>
            {
                if (!_active)
                {
                    Logger.LogWarning("YGuardNoFF idle — reload ignored");
                    return;
                }
                _ = SyncAndApplyAsync();
            });
        AddCommand(
            "css_yguard_nof_status",
            "Print YGuardNoFF status",
            (player, info) =>
            {
                var msg =
                    $"[YGuardNoFF] v{ModuleVersion} active={_active} type={serverType} " +
                    $"para={_parachute} ff={_friendlyFire} bhop={_bunnyHop}";
                Logger.LogInformation("{Msg}", msg);
                info.ReplyToCommand(msg);
                player?.PrintToChat(msg);
            });

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
            "YGuardNoFF {Version} active (SERVER_TYPE={Type})",
            ModuleVersion,
            string.IsNullOrEmpty(serverType) ? "(unset)" : serverType);

        if (hotReload)
        {
            foreach (var p in Utilities.GetPlayers())
            {
                EnsureParaState(p);
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

    private void OnParachuteCommand(CCSPlayerController? player, CounterStrikeSharp.API.Modules.Commands.CommandInfo info)
    {
        var arg = info.ArgByIndex(1);
        if (string.Equals(arg, "1", StringComparison.Ordinal)
            || string.Equals(arg, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "true", StringComparison.OrdinalIgnoreCase))
        {
            _parachute = true;
            var msg = $"[YGuardNoFF] parachute FORCED ON (active={_active} v{ModuleVersion})";
            Logger.LogInformation("{Msg}", msg);
            info.ReplyToCommand(msg);
            Server.PrintToChatAll($" \x10{msg}");
            return;
        }

        if (string.Equals(arg, "0", StringComparison.Ordinal)
            || string.Equals(arg, "off", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "false", StringComparison.OrdinalIgnoreCase))
        {
            _parachute = false;
            Server.NextFrame(StopAllParachutes);
            var msg = $"[YGuardNoFF] parachute FORCED OFF (v{ModuleVersion})";
            Logger.LogInformation("{Msg}", msg);
            info.ReplyToCommand(msg);
            return;
        }

        var status =
            $"[YGuardNoFF] v{ModuleVersion} active={_active} para={_parachute} ff={_friendlyFire} bhop={_bunnyHop}";
        info.ReplyToCommand(status);
        Logger.LogInformation("{Msg}", status);
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
        if (!_active || !_parachute)
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

            if (IsHoldingUse(player, pawn) && IsAirborne(pawn))
            {
                StartPara(player, pawn);
            }
            else if (_usingPara.TryGetValue(index, out var usingPara) && usingPara)
            {
                _usingPara[index] = false;
                StopPara(player);
            }
        }
    }

    private static bool IsHoldingUse(CCSPlayerController player, CCSPlayerPawn pawn)
    {
        if ((player.Buttons & PlayerButtons.Use) != 0)
        {
            return true;
        }

        // Fallback: some builds expose the live button state on MovementServices.
        try
        {
            var services = pawn.MovementServices;
            if (services is null)
            {
                return false;
            }

            var state = services.Buttons.ButtonStates[0];
            return (state & (ulong)PlayerButtons.Use) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAirborne(CCSPlayerPawn pawn)
    {
        if (!pawn.OnGroundLastTick)
        {
            return true;
        }

        // Fallback ground checks — OnGroundLastTick has been flaky on some builds.
        if ((pawn.Flags & (uint)PlayerFlags.FL_ONGROUND) == 0)
        {
            return true;
        }

        try
        {
            return !pawn.GroundEntity.IsValid;
        }
        catch
        {
            return false;
        }
    }

    private void StartPara(CCSPlayerController player, CCSPlayerPawn pawn)
    {
        var index = (int)player.Index;
        if (!_usingPara.TryGetValue(index, out var usingPara) || !usingPara)
        {
            _usingPara[index] = true;
            // le1t / working CS2 builds: GravityScale on the PAWN.
            pawn.GravityScale = 0.1f;
            player.GravityScale = 0.1f;
            player.PrintToChat(" \x04[YGuard] Parachute");
        }

        var fallspeed = FallSpeed * -1.0f;
        var velocity = pawn.AbsVelocity;
        if (velocity.Z >= 0.0f)
        {
            return;
        }

        var isFallSpeed = velocity.Z >= fallspeed;
        if ((isFallSpeed && Linear) || DecreaseVec == 0.0f)
        {
            velocity.Z = fallspeed;
        }
        else
        {
            velocity.Z += DecreaseVec;
        }

        // Apply velocity every tick (le1t always teleports; pawn is the physical body).
        var origin = pawn.AbsOrigin;
        var angles = pawn.AbsRotation;
        var applied = new Vector(velocity.X, velocity.Y, velocity.Z);
        if (origin is not null && angles is not null)
        {
            pawn.Teleport(origin, angles, applied);
        }
        else
        {
            pawn.Teleport(null, null, applied);
        }

        _paraTicks[index] = (_paraTicks.TryGetValue(index, out var ticks) ? ticks : 0) + 1;
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

        _paraTicks[(int)player.Index] = 0;
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
