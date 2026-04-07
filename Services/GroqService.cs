using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WannaFill.API.Services;

public class GroqService : IGroqService
{
    private readonly HttpClient _http;
    private readonly ILogger<GroqService> _logger;

    public GroqService(HttpClient http, ILogger<GroqService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ParsedProfile> ParseDescripcionAsync(string descripcion, string gameName)
    {
        var prompt =
            $"You are a {gameName} player profile extractor.\n" +
            $"Extract structured data from this player description: \"{descripcion}\"\n\n" +
            "Return ONLY valid JSON with these fields:\n" +
            "- rol: their role/position/class (string or null if not mentioned)\n" +
            "- rango: their rank/division/elo (string or null if not mentioned)\n" +
            "IMPORTANT: For 'rango', only extract known rank names (Iron, Bronze, Silver, Gold, Platinum, Emerald, Diamond, Master, Grandmaster, Challenger, or their Spanish equivalents like Hierro, Bronce, Plata, Oro, Platino, Esmeralda, Diamante, Maestro, Gran Maestro, Retador, etc.). Do NOT extract numbers like '1', '2', '3' as ranks — those are likely referring to queue type (1v1, 2v2) or group size. If no valid rank name is found, set rango to null.\n" +
            "- busca_rol: the role they are looking for in a teammate (string or null)\n" +
            "- busca_rango: the rank they want their teammate to have (string or null)\n" +
            "- estilo: \"ranked\", \"casual\", or \"any\" — infer from context\n" +
            "- idioma: language they play in, e.g. \"es\", \"en\", \"pt\" (string or null)\n" +
            $"- tamaño_equipo_buscado: total team size they want to form (integer or null). Examples: 'busco duo'=2, 'busco trio'=3, 'busco squad'=4, 'somos 2 buscamos 1'=3\n" +
            "- tamaño_grupo_actual: how many players are already in the group (integer or null). Examples: 'somos 2'=2, 'voy solo'=1, 'somos 3 buscamos 2'=3. If not mentioned, set to null.\n" +
            "- modo: the game mode they want to play (string or null). For League of Legends: 'Flex' or 'SoloDuo'. For Valorant: 'Ranked' or 'Casual'. For CS2: 'Competitive' or 'Premier'. For Fortnite: 'Ranked' or 'Casual'. For other games use their equivalent ranked/casual mode keys. If not mentioned, set to null.\n\n" +
            "If a field cannot be determined, use null.\n" +
            "Respond ONLY with valid JSON, no extra text:\n" +
            "{\"rol\":\"support\",\"rango\":\"gold\",\"busca_rol\":\"adc\",\"busca_rango\":\"gold\",\"estilo\":\"ranked\",\"idioma\":\"es\",\"tamaño_equipo_buscado\":2,\"tamaño_grupo_actual\":1,\"modo\":\"Flex\"}";

        var body = new
        {
            model = "llama-3.3-70b-versatile",
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 150,
            temperature = 0.0
        };

        var json = JsonSerializer.Serialize(body);
        var request = new HttpRequestMessage(HttpMethod.Post,
            "https://api.groq.com/openai/v1/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY")
            ?? throw new InvalidOperationException("GROQ_API_KEY not set");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseJson);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        content = content.Trim().TrimStart('`').TrimEnd('`');
        if (content.StartsWith("json")) content = content[4..].Trim();

        using var resultDoc = JsonDocument.Parse(content);
        var root = resultDoc.RootElement;

        string? GetStr(string key) =>
            root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String
                ? el.GetString() : null;

        int? GetInt(string key) =>
            root.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Number
                ? el.GetInt32() : null;

        var profile = new ParsedProfile(
            Rol: GetStr("rol"),
            Rango: GetStr("rango"),
            BuscaRol: GetStr("busca_rol"),
            BuscaRango: GetStr("busca_rango"),
            Estilo: GetStr("estilo") ?? "any",
            Idioma: GetStr("idioma"),
            TamañoEquipoBuscado: GetInt("tamaño_equipo_buscado"),
            Modo: GetStr("modo"),
            TamañoGrupoActual: GetInt("tamaño_grupo_actual")
        );

        _logger.LogInformation("Parsed profile for [{Desc}]: {@Profile}", descripcion, profile);

        return profile;
    }
}
