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
            Name: "CS2",
            Slug: "cs2",
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
        ),
        new GameDefinition(
            Id: 5,
            Name: "Apex Legends",
            Slug: "apex",
            Servers: new[] { "NA", "EU", "SA", "AS", "OCE" },
            Modes: new[]
            {
                new ModeDefinition("Casual", "Casual", 3, false),
                new ModeDefinition("Ranked", "Ranked", 3, true)
            },
            TeamFormats: null,
            Ranks: new[] { "Rookie", "Bronze", "Silver", "Gold", "Platinum", "Diamond", "Master", "Predator" }
        ),
        new GameDefinition(
            Id: 6,
            Name: "Rocket League",
            Slug: "rocket-league",
            Servers: new[] { "US-East", "US-West", "EU", "Asia-East", "Asia-SE", "Middle East", "Oceania", "SA" },
            Modes: new[]
            {
                new ModeDefinition("Casual", "Casual", 3, false),
                new ModeDefinition("Ranked1v1", "Ranked 1v1", 1, true),
                new ModeDefinition("Ranked2v2", "Ranked 2v2", 2, true),
                new ModeDefinition("Ranked3v3", "Ranked 3v3", 3, true)
            },
            TeamFormats: null,
            Ranks: new[] { "Bronze", "Silver", "Gold", "Platinum", "Diamond", "Champion", "Grand Champion", "Supersonic Legend" }
        ),
        new GameDefinition(
            Id: 7,
            Name: "Dota 2",
            Slug: "dota2",
            Servers: new[] { "US-East", "US-West", "EU-West", "EU-East", "Russia", "SE-Asia", "Australia", "SA", "Dubai", "India" },
            Modes: new[]
            {
                new ModeDefinition("AllPick", "All Pick", 5, true),
                new ModeDefinition("Turbo", "Turbo", 5, false)
            },
            TeamFormats: null,
            Ranks: new[] { "Herald", "Guardian", "Crusader", "Archon", "Legend", "Ancient", "Divine", "Immortal" }
        ),
        new GameDefinition(
            Id: 8,
            Name: "Overwatch 2",
            Slug: "overwatch2",
            Servers: new[] { "Americas", "Europe", "Asia" },
            Modes: new[]
            {
                new ModeDefinition("Quickplay", "Quick Play", 5, false),
                new ModeDefinition("Competitive", "Competitive", 5, true)
            },
            TeamFormats: null,
            Ranks: new[] { "Bronze", "Silver", "Gold", "Platinum", "Diamond", "Master", "Grandmaster", "Top 500" }
        ),
        new GameDefinition(
            Id: 9,
            Name: "Rainbow Six Siege",
            Slug: "r6siege",
            Servers: new[] { "NA", "EU", "LATAM", "Asia" },
            Modes: new[]
            {
                new ModeDefinition("Casual", "Casual", 5, false),
                new ModeDefinition("Ranked", "Ranked", 5, true)
            },
            TeamFormats: null,
            Ranks: new[] { "Copper", "Bronze", "Silver", "Gold", "Platinum", "Diamond", "Champion" }
        ),
        new GameDefinition(
            Id: 10,
            Name: "EA FC 25",
            Slug: "eafc25",
            Servers: new[] { "Global" },
            Modes: new[]
            {
                new ModeDefinition("FUT", "FUT Rivals", 1, true),
                new ModeDefinition("ProClubs", "Pro Clubs", 11, false)
            },
            TeamFormats: null,
            Ranks: new[] { "Division 10", "Division 9", "Division 8", "Division 7", "Division 6", "Division 5", "Division 4", "Division 3", "Division 2", "Division 1", "Elite" }
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
