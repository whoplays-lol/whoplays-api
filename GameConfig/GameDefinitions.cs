namespace WannaFill.API.GameConfig;

public record GameDefinition(
    int Id,
    string Name,
    string Slug,
    string[] Servers,
    ModeDefinition[] Modes,
    TeamFormatDefinition[]? TeamFormats,
    string[] Ranks
);

public record ModeDefinition(
    string Key,
    string Name,
    int TeamSize, // 0 = depends on TeamFormat
    bool RankRequired
);

public record TeamFormatDefinition(
    string Key,
    string Name,
    int TeamSize
);

public static class GameDefinitions
{
    public static readonly GameDefinition[] All = new[]
    {
        new GameDefinition(
            Id: 1,
            Name: "League of Legends",
            Slug: "lol",
            Servers: new[] { "NA", "EUW", "EUNE", "LAN", "LAS", "BR", "OCE", "KR", "JP", "TR", "RU" },
            Modes: new[]
            {
                new ModeDefinition("Flex", "Flex", 5, true),
                new ModeDefinition("SoloDuo", "Solo/Duo", 2, true)
            },
            TeamFormats: null,
            Ranks: new[] { "Iron", "Bronze", "Silver", "Gold", "Platinum", "Emerald", "Diamond", "Master", "Grandmaster", "Challenger" }
        ),
        new GameDefinition(
            Id: 2,
            Name: "Valorant",
            Slug: "valorant",
            Servers: new[] { "NA", "EU", "LATAM", "BR", "AP", "KR" },
            Modes: new[]
            {
                new ModeDefinition("Casual", "Casual", 5, false),
                new ModeDefinition("Ranked", "Ranked", 5, true)
            },
            TeamFormats: null,
            Ranks: new[] { "Iron", "Bronze", "Silver", "Gold", "Platinum", "Diamond", "Ascendant", "Immortal", "Radiant" }
        ),
        new GameDefinition(
            Id: 3,
            Name: "CS:GO",
            Slug: "csgo",
            Servers: new[] { "EU", "NA", "SA", "Asia", "Australia" },
            Modes: new[]
            {
                new ModeDefinition("Competitive", "Competitive", 5, true)
            },
            TeamFormats: null,
            Ranks: new[] { "Silver I", "Silver II", "Silver III", "Silver IV", "Silver Elite", "Silver Elite Master", "Gold Nova I", "Gold Nova II", "Gold Nova III", "Gold Nova Master", "Master Guardian I", "Master Guardian II", "Legendary Eagle", "Legendary Eagle Master", "Supreme", "Global Elite" }
        ),
        new GameDefinition(
            Id: 4,
            Name: "Fortnite",
            Slug: "fortnite",
            Servers: new[] { "NA-East", "NA-West", "EU", "BR", "OCE", "Asia", "ME" },
            Modes: new[]
            {
                new ModeDefinition("Casual", "Casual", 0, false),
                new ModeDefinition("Ranked", "Ranked", 0, true)
            },
            TeamFormats: new[]
            {
                new TeamFormatDefinition("Duo", "Duo", 2),
                new TeamFormatDefinition("Trio", "Trio", 3),
                new TeamFormatDefinition("Squad", "Squad", 4)
            },
            Ranks: new[] { "Bronze", "Silver", "Gold", "Platinum", "Diamond", "Elite", "Champion", "Unreal" }
        )
    };

    public static GameDefinition? GetById(int id) => All.FirstOrDefault(g => g.Id == id);

    public static int GetTeamSize(int gameId, string mode, string? teamFormat)
    {
        var game = GetById(gameId) ?? throw new InvalidOperationException($"Game {gameId} not found");
        var modeDef = game.Modes.FirstOrDefault(m => m.Key == mode)
            ?? throw new InvalidOperationException($"Mode {mode} not found for game {gameId}");

        if (modeDef.TeamSize > 0) return modeDef.TeamSize;

        // TeamSize depends on TeamFormat (Fortnite)
        if (game.TeamFormats == null || teamFormat == null)
            throw new InvalidOperationException("TeamFormat required for this game/mode");

        var format = game.TeamFormats.FirstOrDefault(f => f.Key == teamFormat)
            ?? throw new InvalidOperationException($"TeamFormat {teamFormat} not found");

        return format.TeamSize;
    }

    public static bool IsRankRequired(int gameId, string mode)
    {
        var game = GetById(gameId);
        var modeDef = game?.Modes.FirstOrDefault(m => m.Key == mode);
        return modeDef?.RankRequired ?? false;
    }

    public static bool IsRankCompatible(int gameId, string? rankA, string? rankB)
    {
        var game = GetById(gameId);
        if (game == null || game.Ranks.Length == 0) return true;
        if (rankA == null && rankB == null) return true;
        if (rankA == null || rankB == null) return false;

        var indexA = Array.IndexOf(game.Ranks, rankA);
        var indexB = Array.IndexOf(game.Ranks, rankB);

        if (indexA < 0 || indexB < 0) return false;

        // Allow ±1 rank tolerance
        return Math.Abs(indexA - indexB) <= 1;
    }
}
