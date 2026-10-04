using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace YGuardRanks;

/// <summary>
/// Public-server ladder: points on the panel, Competitive skill-group icons on TAB.
/// Idle on Ranked / Practice.
/// </summary>
public class YGuardRanksPlugin : BasePlugin, IPluginConfig<YGuardRanksConfig>
{
    public override string ModuleName => "YGuard Ranks";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "YGuard";
    public override string ModuleDescription =>
        "Public server points + Competitive rank icons on the scoreboard";

    public YGuardRanksConfig Config { get; set; } = new();

    private readonly PanelApi _api = new();
    private bool _enabled;
    private bool _scoreboardOk = true;

    // Competitive skill-group type (icons 1–18). Not Premier (11).
    private const int RankTypeCompetitive = 12;
    private const int MinWinsForVisibility = 10;
    private const string RankRevealAllMessage = "CCSUsrMsg_ServerRankRevealAll";

    private readonly Dictionary<ulong, PlayerRank> _cache = [];
    private readonly List<PendingEvent> _pending = [];
    private readonly object _pendingLock = new();
    private (CCSPlayerController Player, int Skill)[] _tracked = [];

    public void OnConfigParsed(YGuardRanksConfig config)
    {
        config.MinPlayersForPoints = Math.Clamp(config.MinPlayersForPoints, 0, 64);
        config.SyncSeconds = Math.Clamp(config.SyncSeconds, 5, 300);
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        var serverType = (Environment.GetEnvironmentVariable("SERVER_TYPE") ?? "").Trim();
        _enabled = ResolveEnabled(serverType, Config.PublicOnly);
        if (!_enabled)
        {
            Logger.LogInformation("YGuardRanks idle (SERVER_TYPE={Type})", serverType);
            return;
        }

        if (IsFollowCS2ServerGuidelinesBlocking())
        {
            _scoreboardOk = false;
            Logger.LogWarning(
                "Scoreboard ranks disabled: set FollowCS2ServerGuidelines=false in counterstrikesharp/configs/core.json");
        }

        RegisterListener<Listeners.OnClientAuthorized>(OnClientAuthorized);
        RegisterListener<Listeners.OnMapStart>(_ =>
        {
            PullOnline();
            AddTimer(1.0f, () =>
            {
                RefreshRoster();
                SendRevealAll();
            });
        });

        if (Config.ShowScoreboardRanks && _scoreboardOk)
        {
            RegisterListener<Listeners.OnTick>(OnTick);
            AddTimer(1.0f, () =>
            {
                RefreshRoster();
                SendRevealAll();
            }, TimerFlags.REPEAT);
        }

        AddTimer(Config.SyncSeconds, () => _ = FlushAsync(), TimerFlags.REPEAT);
        AddTimer(2.0f, PullOnline);

        Logger.LogInformation(
            "YGuardRanks active (api={Api}, scoreboard={Sb})",
            _api.Configured,
            Config.ShowScoreboardRanks && _scoreboardOk);
    }

    private static bool ResolveEnabled(string serverType, bool publicOnly)
    {
        if (!publicOnly) return true;
        if (string.IsNullOrEmpty(serverType)) return true;
        return !serverType.Equals("Ranked", StringComparison.OrdinalIgnoreCase)
            && !serverType.Equals("Practice", StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- events

    private void OnClientAuthorized(int slot, SteamID steamId)
    {
        if (!_enabled || steamId.SteamId64 == 0) return;
        var sid = steamId.SteamId64;
        _ = Task.Run(async () =>
        {
            try
            {
                var list = await _api.GetPlayersAsync([sid]);
                var row = list.FirstOrDefault();
                Server.NextFrame(() =>
                {
                    if (row != null)
                        ApplyDto(row);
                    else if (!_cache.ContainsKey(sid))
                        _cache[sid] = new PlayerRank { SteamId = sid };
                    RefreshRoster();
                    SendRevealAll();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[YGuardRanks] load {sid}: {ex.Message}");
            }
        });
    }

    [GameEventHandler]
    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo _)
    {
        if (!_enabled || !PointsActive()) return HookResult.Continue;

        var victim = @event.Userid;
        var attacker = @event.Attacker;
        var assister = @event.Assister;

        if (victim is { IsValid: true, IsHLTV: false } && !victim.IsBot)
            Award(victim, Config.PointsDeath, kills: 0, deaths: 1, assists: 0, headshots: 0);

        if (attacker is { IsValid: true, IsHLTV: false }
            && attacker != victim
            && (Config.PointsForBots || !attacker.IsBot)
            && (Config.PointsForBots || victim is not { IsBot: true }))
        {
            var hs = @event.Headshot ? Config.PointsHeadshot : 0;
            Award(attacker, Config.PointsKill + hs, kills: 1, deaths: 0, assists: 0,
                headshots: @event.Headshot ? 1 : 0);
        }

        if (assister is { IsValid: true, IsHLTV: false }
            && assister != attacker
            && assister != victim
            && (Config.PointsForBots || !assister.IsBot))
        {
            Award(assister, Config.PointsAssist, kills: 0, deaths: 0, assists: 1, headshots: 0);
        }

        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundMvp(EventRoundMvp @event, GameEventInfo _)
    {
        if (!_enabled || !PointsActive()) return HookResult.Continue;
        var player = @event.Userid;
        if (player is { IsValid: true, IsHLTV: false } && !player.IsBot)
            Award(player, Config.PointsMvp, 0, 0, 0, 0);
        return HookResult.Continue;
    }

    [GameEventHandler]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo _)
    {
        if (!_enabled || !PointsActive() || Config.PointsRoundWin == 0)
            return HookResult.Continue;

        var winner = (CsTeam)@event.Winner;
        if (winner is not (CsTeam.Terrorist or CsTeam.CounterTerrorist))
            return HookResult.Continue;

        foreach (var player in Utilities.GetPlayers())
        {
            if (player is not { IsValid: true, IsHLTV: false } || player.IsBot) continue;
            if ((CsTeam)player.TeamNum != winner) continue;
            Award(player, Config.PointsRoundWin, 0, 0, 0, 0);
        }
        return HookResult.Continue;
    }

    // ---------------------------------------------------------------- commands

    [ConsoleCommand("css_rank", "Show your public rank")]
    [ConsoleCommand("css_myrank", "Show your public rank")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnRank(CCSPlayerController? player, CommandInfo _)
    {
        if (!_enabled || player is not { IsValid: true }) return;
        var rank = GetOrCreate(player.SteamID, player.PlayerName);
        var (skill, name) = RankLadder.FromPoints(rank.Points);
        player.PrintToChat(
            $" {ChatColors.Gold}[{Config.ChatPrefix}]{ChatColors.Default} {name} " +
            $"{ChatColors.Grey}({skill}/18){ChatColors.Default} · {ChatColors.Green}{rank.Points}{ChatColors.Default} pts · " +
            $"K/D {rank.Kills}/{rank.Deaths}");
    }

    [ConsoleCommand("css_top", "Show public top ranks")]
    [ConsoleCommand("css_ranktop", "Show public top ranks")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnTop(CCSPlayerController? player, CommandInfo info)
    {
        if (!_enabled || player is not { IsValid: true }) return;
        var caller = player;
        Task.Run(async () =>
        {
            List<RankPlayerDto> top;
            try { top = await _api.GetTopAsync(10); }
            catch { top = []; }

            Server.NextFrame(() =>
            {
                if (caller is not { IsValid: true }) return;
                if (top.Count == 0)
                {
                    caller.PrintToChat(
                        $" {ChatColors.Gold}[{Config.ChatPrefix}]{ChatColors.Default} No ladder data yet.");
                    return;
                }
                caller.PrintToChat(
                    $" {ChatColors.Gold}[{Config.ChatPrefix}]{ChatColors.Default} Top public ranks:");
                var i = 1;
                foreach (var row in top)
                {
                    caller.PrintToChat(
                        $" {ChatColors.Grey}#{i++}{ChatColors.Default} {row.Name ?? row.SteamId} " +
                        $"{ChatColors.Gold}{row.RankName}{ChatColors.Default} · {row.Points} pts");
                }
            });
        });
    }

    // ---------------------------------------------------------------- scoreboard

    private void OnTick()
    {
        var tracked = _tracked;
        if (tracked.Length == 0) return;
        try
        {
            foreach (var (player, skill) in tracked)
            {
                if (!player.IsValid) continue;
                SetCompetitiveRank(player, skill);
            }
        }
        catch (Exception ex)
        {
            _tracked = [];
            _scoreboardOk = false;
            Logger.LogError(ex, "YGuardRanks.OnTick failed; scoreboard disabled");
        }
    }

    private void RefreshRoster()
    {
        if (!Config.ShowScoreboardRanks || !_scoreboardOk)
        {
            _tracked = [];
            return;
        }

        try
        {
            var list = new List<(CCSPlayerController, int)>();
            foreach (var player in Utilities.GetPlayers())
            {
                if (player is not { IsValid: true, IsHLTV: false } || player.IsBot) continue;
                if (!_cache.TryGetValue(player.SteamID, out var rank)) continue;
                var (skill, _) = RankLadder.FromPoints(rank.Points);
                list.Add((player, skill));
            }
            _tracked = list.ToArray();
        }
        catch (Exception ex)
        {
            _tracked = [];
            Logger.LogError(ex, "YGuardRanks.RefreshRoster failed");
        }
    }

    private void SendRevealAll()
    {
        if (!Config.ShowScoreboardRanks || !_scoreboardOk) return;
        try
        {
            using var message = UserMessage.FromPartialName(RankRevealAllMessage);
            message.Recipients.AddAllPlayers();
            message.Send();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "YGuardRanks: reveal-all failed");
        }
    }

    private static void SetCompetitiveRank(CCSPlayerController player, int skill)
    {
        skill = Math.Clamp(skill, 1, 18);
        if (player.CompetitiveRanking == skill
            && player.CompetitiveRankType == (sbyte)RankTypeCompetitive
            && player.CompetitiveWins == MinWinsForVisibility)
        {
            return;
        }

        player.CompetitiveRankType = (sbyte)RankTypeCompetitive;
        player.CompetitiveRanking = skill;
        player.CompetitiveWins = MinWinsForVisibility;

        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveRankType");
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveRanking");
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iCompetitiveWins");
    }

    // ---------------------------------------------------------------- points / sync

    private bool PointsActive()
    {
        if (!Config.WarmupPoints)
        {
            // GameRules may be null early; treat as active if unavailable.
            try
            {
                var rules = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
                    .FirstOrDefault()?.GameRules;
                if (rules?.WarmupPeriod == true) return false;
            }
            catch { /* ignore */ }
        }

        var humans = 0;
        foreach (var p in Utilities.GetPlayers())
        {
            if (p is { IsValid: true, IsHLTV: false, IsBot: false }
                && p.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist)
            {
                humans++;
            }
        }
        return humans >= Config.MinPlayersForPoints;
    }

    private void Award(
        CCSPlayerController player, int delta, int kills, int deaths, int assists, int headshots)
    {
        if (delta == 0 && kills == 0 && deaths == 0 && assists == 0 && headshots == 0) return;
        var rank = GetOrCreate(player.SteamID, player.PlayerName);
        rank.Points = Math.Max(0, rank.Points + delta);
        rank.Kills += kills;
        rank.Deaths += deaths;
        rank.Assists += assists;
        rank.Headshots += headshots;
        rank.Name = player.PlayerName;

        lock (_pendingLock)
        {
            _pending.Add(new PendingEvent
            {
                SteamId = player.SteamID,
                Name = player.PlayerName,
                DeltaPoints = delta,
                Kills = kills,
                Deaths = deaths,
                Assists = assists,
                Headshots = headshots,
            });
        }
    }

    private PlayerRank GetOrCreate(ulong steamId, string? name)
    {
        if (!_cache.TryGetValue(steamId, out var rank))
        {
            rank = new PlayerRank { SteamId = steamId, Name = name };
            _cache[steamId] = rank;
        }
        return rank;
    }

    private void ApplyDto(RankPlayerDto dto)
    {
        if (!ulong.TryParse(dto.SteamId, out var sid) || sid == 0) return;
        _cache[sid] = new PlayerRank
        {
            SteamId = sid,
            Name = dto.Name,
            Points = dto.Points,
            Kills = dto.Kills,
            Deaths = dto.Deaths,
            Assists = dto.Assists,
            Headshots = dto.Headshots,
        };
    }

    private void PullOnline()
    {
        if (!_enabled || !_api.Configured) return;
        var ids = Utilities.GetPlayers()
            .Where(p => p is { IsValid: true, IsHLTV: false, IsBot: false } && p.SteamID != 0)
            .Select(p => p.SteamID)
            .Distinct()
            .ToList();
        if (ids.Count == 0) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var list = await _api.GetPlayersAsync(ids);
                Server.NextFrame(() =>
                {
                    foreach (var row in list) ApplyDto(row);
                    RefreshRoster();
                    SendRevealAll();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[YGuardRanks] pull: {ex.Message}");
            }
        });
    }

    private async Task FlushAsync()
    {
        if (!_enabled || !_api.Configured) return;
        List<PendingEvent> batch;
        lock (_pendingLock)
        {
            if (_pending.Count == 0) return;
            batch = [.._pending];
            _pending.Clear();
        }

        // Collapse per steam id for one upsert row each.
        var merged = batch
            .GroupBy(e => e.SteamId)
            .Select(g => new
            {
                steam_id = g.Key.ToString(),
                name = g.Last().Name,
                delta_points = g.Sum(x => x.DeltaPoints),
                kills = g.Sum(x => x.Kills),
                deaths = g.Sum(x => x.Deaths),
                assists = g.Sum(x => x.Assists),
                headshots = g.Sum(x => x.Headshots),
            })
            .Cast<object>()
            .ToList();

        try
        {
            var updated = await _api.SyncAsync(merged);
            Server.NextFrame(() =>
            {
                foreach (var row in updated) ApplyDto(row);
                RefreshRoster();
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[YGuardRanks] flush: {ex.Message}");
            lock (_pendingLock) _pending.InsertRange(0, batch);
        }
    }

    private static bool IsFollowCS2ServerGuidelinesBlocking()
    {
        var configPath = Path.Join(
            Server.GameDirectory, "csgo", "addons", "counterstrikesharp", "configs", "core.json");
        if (!File.Exists(configPath)) return false;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            return doc.RootElement.TryGetProperty("FollowCS2ServerGuidelines", out var prop)
                && prop.ValueKind == JsonValueKind.True;
        }
        catch { return false; }
    }

    private sealed class PlayerRank
    {
        public ulong SteamId;
        public string? Name;
        public int Points;
        public int Kills;
        public int Deaths;
        public int Assists;
        public int Headshots;
    }

    private sealed class PendingEvent
    {
        public ulong SteamId;
        public string? Name;
        public int DeltaPoints;
        public int Kills;
        public int Deaths;
        public int Assists;
        public int Headshots;
    }
}
