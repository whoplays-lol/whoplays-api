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

    public async Task<CompatibilityResult> CheckCompatibilityAsync(
        string descripcionA,
        string descripcionB,
        string gameName)
    {
        var prompt =
            $"You are an extremely strict {gameName} ranked matchmaking evaluator.\n" +
            $"Both players want to find a teammate in {gameName}.\n\n" +
            $"Player A: \"{descripcionA}\"\n" +
            $"Player B: \"{descripcionB}\"\n\n" +
            "MUTUAL NEED RULE (check this first):\n" +
            "- Determine what each player OFFERS (role, group size, skill) and what they NEED (role, group size).\n" +
            "- A valid match means A's offer fills B's need AND B's offer fills A's need.\n" +
            "- If both players need the same thing (e.g. both are groups of 2 looking for 3 more, both need an ADC, both need a support) → they CANNOT fill each other's need → compatible=false, score=1.\n" +
            "- If both players are solo and each is looking for exactly 1 person → they CAN fill each other's need → proceed to other rules.\n\n" +
            "RANK COMPATIBILITY RULE (apply after mutual need check):\n" +
            "- Extract rank/tier from each description. Tier order low→high: Iron, Bronze, Silver, Gold, Platinum, Emerald, Diamond, Master, Grandmaster, Challenger (adapt for other games).\n" +
            "- If rank difference is MORE than 1 tier → compatible=false, score=1. No exceptions.\n" +
            "- If one mentions rank and the other doesn't, and the mentioned rank is Diamond+ → incompatible.\n" +
            "- If neither mentions rank → neutral, continue.\n\n" +
            "SECONDARY RULES (only if both above pass):\n" +
            "1. Role/playstyle compatibility — incompatible roles: -2\n" +
            "2. Schedule/timezone/language mismatch: -1\n" +
            "3. One casual, one tryhard: -2\n\n" +
            "SCORING: Start at 10, subtract per issue. compatible=true ONLY if score >= 7 AND all primary rules passed.\n" +
            "If text is nonsense or unrelated to gaming → compatible=false, score=0.\n\n" +
            "Respond ONLY with valid JSON, no extra text: {\"compatible\":true,\"score\":8,\"reason\":\"one sentence explaining decision\"}";

        var body = new
        {
            model = "llama-3.3-70b-versatile",
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 100,
            temperature = 0.1
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
        _logger.LogInformation("Groq raw response: {Response}", responseJson);

        using var doc = JsonDocument.Parse(responseJson);

        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        content = content.Trim().TrimStart('`').TrimEnd('`');
        if (content.StartsWith("json")) content = content[4..].Trim();

        using var resultDoc = JsonDocument.Parse(content);
        var compatible = resultDoc.RootElement.GetProperty("compatible").GetBoolean();
        var score = resultDoc.RootElement.GetProperty("score").GetInt32();
        var reason = resultDoc.RootElement.GetProperty("reason").GetString() ?? "";

        _logger.LogInformation("Groq compatibility: compatible={Compatible} score={Score} reason={Reason}",
            compatible, score, reason);

        return new CompatibilityResult(compatible, score, reason);
    }
}
