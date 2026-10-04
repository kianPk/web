using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace YGuardRanks;

public class YGuardRanksConfig : BasePluginConfig
{
    [JsonPropertyName("ChatPrefix")]
    public string ChatPrefix { get; set; } = "YGuard";

    /// <summary>Idle on Ranked / Practice pods.</summary>
    [JsonPropertyName("PublicOnly")]
    public bool PublicOnly { get; set; } = true;

    /// <summary>
    /// Humans in T/CT required before points move. 1 = solo vs bots is fine.
    /// </summary>
    [JsonPropertyName("MinPlayersForPoints")]
    public int MinPlayersForPoints { get; set; } = 1;

    [JsonPropertyName("PointsKill")]
    public int PointsKill { get; set; } = 5;

    [JsonPropertyName("PointsHeadshot")]
    public int PointsHeadshot { get; set; } = 2;

    [JsonPropertyName("PointsAssist")]
    public int PointsAssist { get; set; } = 2;

    [JsonPropertyName("PointsDeath")]
    public int PointsDeath { get; set; } = -2;

    [JsonPropertyName("PointsMvp")]
    public int PointsMvp { get; set; } = 3;

    [JsonPropertyName("PointsRoundWin")]
    public int PointsRoundWin { get; set; } = 1;

    /// <summary>Award points for killing / dying to bots (typical on public fills).</summary>
    [JsonPropertyName("PointsForBots")]
    public bool PointsForBots { get; set; } = true;

    /// <summary>Print +N / −N in chat when points change.</summary>
    [JsonPropertyName("ShowPointMessages")]
    public bool ShowPointMessages { get; set; } = true;

    [JsonPropertyName("WarmupPoints")]
    public bool WarmupPoints { get; set; } = false;

    /// <summary>How often pending point deltas are flushed to the panel.</summary>
    [JsonPropertyName("SyncSeconds")]
    public int SyncSeconds { get; set; } = 20;

    /// <summary>Show Competitive skill-group icons (1–18) on the TAB scoreboard.</summary>
    [JsonPropertyName("ShowScoreboardRanks")]
    public bool ShowScoreboardRanks { get; set; } = true;
}
