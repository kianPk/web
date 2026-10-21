namespace YGuardAC;

/// <summary>
/// High-confidence unique product tokens. Short / generic words (aim, hack,
/// meme, sense) are intentionally omitted to avoid false positives.
/// </summary>
internal static class CheatBrandTokens
{
    public static readonly string[] Unique =
    [
        "memesense",
        "meme-sense",
        "meme_sense",
        "meme sense",
        "memsense",
        "neverlose",
        "fatality",
        "nixware",
        "primordial",
        "gamesense",
        "onetap",
        "aimjunkies",
        "spirthack",
        "interium",
        "rawetrip",
        "exloader",
        "undetek",
        "iniuria",
        "superluminal",
        "asphyxia",
        "aimware",
        "midnight.im",
        "skeet.cc",
        "phantomoverlay",
        "cs2internal",
        "cs2external",
        "never-lose",
        "fatalitycs",
    ];

    public static bool Matches(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.ToLowerInvariant();
        foreach (var tok in Unique)
        {
            if (tok.Length >= 7 && t.Contains(tok, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public static string? FirstMatch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.ToLowerInvariant();
        foreach (var tok in Unique)
        {
            if (tok.Length >= 7 && t.Contains(tok, StringComparison.Ordinal))
                return tok;
        }
        return null;
    }

    public static string Canonical(string token)
    {
        var t = token.ToLowerInvariant()
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(".", "", StringComparison.Ordinal);
        if (t.Contains("memsense", StringComparison.Ordinal) ||
            t.Contains("memesense", StringComparison.Ordinal))
            return "memesense";
        if (t.Contains("neverlose", StringComparison.Ordinal)) return "neverlose";
        if (t.Contains("fatality", StringComparison.Ordinal)) return "fatality";
        if (t.Contains("nixware", StringComparison.Ordinal)) return "nixware";
        if (t.Contains("onetap", StringComparison.Ordinal)) return "onetap";
        if (t.Contains("exloader", StringComparison.Ordinal)) return "exloader";
        if (t.Contains("undetek", StringComparison.Ordinal)) return "undetek";
        if (t.Contains("aimware", StringComparison.Ordinal)) return "aimware";
        if (t.Contains("iniuria", StringComparison.Ordinal)) return "iniuria";
        if (t.Contains("superluminal", StringComparison.Ordinal)) return "superluminal";
        if (t.Contains("asphyxia", StringComparison.Ordinal)) return "asphyxia";
        return string.IsNullOrWhiteSpace(t) ? "cheat" : t;
    }
}
