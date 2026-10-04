namespace YGuardRanks;

/// <summary>Must stay in sync with api PublicRanksService.skillFromPoints.</summary>
internal static class RankLadder
{
    private static readonly (int Points, int Skill, string Name)[] Thresholds =
    [
        (0, 1, "Silver I"),
        (100, 2, "Silver II"),
        (250, 3, "Silver III"),
        (450, 4, "Silver IV"),
        (700, 5, "Silver Elite"),
        (1000, 6, "Silver Elite Master"),
        (1400, 7, "Gold Nova I"),
        (1900, 8, "Gold Nova II"),
        (2500, 9, "Gold Nova III"),
        (3200, 10, "Gold Nova Master"),
        (4000, 11, "Master Guardian I"),
        (5000, 12, "Master Guardian II"),
        (6200, 13, "Master Guardian Elite"),
        (7600, 14, "Distinguished Master Guardian"),
        (9200, 15, "Legendary Eagle"),
        (11000, 16, "Legendary Eagle Master"),
        (13000, 17, "Supreme Master First Class"),
        (15500, 18, "The Global Elite"),
    ];

    public static (int Skill, string Name) FromPoints(int points)
    {
        var p = Math.Max(0, points);
        var current = Thresholds[0];
        foreach (var row in Thresholds)
        {
            if (p >= row.Points) current = row;
            else break;
        }
        return (current.Skill, current.Name);
    }
}
