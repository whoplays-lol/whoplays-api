namespace WannaFill.API.Services;

public record ParsedProfile(
    string? Rol,
    string? Rango,
    string? BuscaRol,
    string? BuscaRango,
    string Estilo,       // "ranked" | "casual" | "any"
    string? Idioma,
    int? TamañoEquipoBuscado,  // total team size the player wants, e.g. 3 for trio
    string? Modo,  // game mode inferred from description e.g. "Flex", "SoloDuo", "Ranked", "Casual"
    int? TamañoGrupoActual  // how many are already in the group, e.g. "somos 2" = 2
);

public interface IGroqService
{
    Task<ParsedProfile> ParseDescripcionAsync(string descripcion, string gameName);
}
