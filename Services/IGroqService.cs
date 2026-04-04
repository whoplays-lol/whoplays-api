namespace WannaFill.API.Services;

public record CompatibilityResult(bool Compatible, int Score, string Reason);

public interface IGroqService
{
    // Compara dos descripciones libres dentro del mismo juego/servidor
    Task<CompatibilityResult> CheckCompatibilityAsync(
        string descripcionA,
        string descripcionB,
        string gameName);
}
