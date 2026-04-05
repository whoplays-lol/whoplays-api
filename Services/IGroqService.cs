namespace WannaFill.API.Services;

public record ParsedProfile(
    string? Rol,
    string? Rango,
    string? BuscaRol,
    string? BuscaRango,
    string Estilo,       // "ranked" | "casual" | "any"
    string? Idioma,
    int? TamañoEquipoBuscado  // total team size the player wants, e.g. 3 for trio
);

public interface IGroqService
{
    Task<ParsedProfile> ParseDescripcionAsync(string descripcion, string gameName);
}
