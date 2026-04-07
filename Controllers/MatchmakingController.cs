using Microsoft.AspNetCore.Mvc;
using WannaFill.API.DTOs;
using WannaFill.API.GameConfig;
using WannaFill.API.Services;

namespace WannaFill.API.Controllers;

public record ParseDescripcionRequest(string Descripcion, int? GameId, string? GameName);

[ApiController]
[Route("api/[controller]")]
public class MatchmakingController : ControllerBase
{
    private readonly IMatchmakingService _matchmaking;
    private readonly IGroqService _groq;
    private readonly ILogger<MatchmakingController> _logger;

    public MatchmakingController(IMatchmakingService matchmaking, IGroqService groq, ILogger<MatchmakingController> logger)
    {
        _matchmaking = matchmaking;
        _groq = groq;
        _logger = logger;
    }

    /// <summary>POST /api/matchmaking/queue — Join the matchmaking queue.</summary>
    [HttpPost("queue")]
    public async Task<IActionResult> Enqueue([FromBody] CreateQueueRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var result = await _matchmaking.EnqueueAsync(dto);
            return CreatedAtAction(nameof(GetQueueRequest), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>GET /api/matchmaking/queue/{id} — Get current status of a queue request.</summary>
    [HttpGet("queue/{id:guid}")]
    public async Task<IActionResult> GetQueueRequest(Guid id)
    {
        var result = await _matchmaking.GetQueueRequestAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>DELETE /api/matchmaking/queue/{id} — Cancel a queue request.</summary>
    [HttpDelete("queue/{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, [FromHeader(Name = "X-Session-Id")] string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return BadRequest(new { error = "X-Session-Id header required" });

        var success = await _matchmaking.CancelAsync(id, sessionId);
        return success ? NoContent() : NotFound();
    }

    /// <summary>GET /api/matchmaking/match/{matchGroupId} — Get match details.</summary>
    [HttpGet("match/{matchGroupId:guid}")]
    public async Task<IActionResult> GetMatch(Guid matchGroupId)
    {
        var result = await _matchmaking.GetMatchGroupAsync(matchGroupId);
        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>GET /api/matchmaking/stats — Players searching per game.</summary>
    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        var stats = _matchmaking.GetQueueStats();
        var descriptions = _matchmaking.GetQueueDescriptions();
        return Ok(new { stats, descriptions });
    }

    /// <summary>POST /api/matchmaking/parse — Parse a player description and return profile + validation warnings.</summary>
    [HttpPost("parse")]
    public async Task<IActionResult> ParseDescripcion([FromBody] ParseDescripcionRequest dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Descripcion) || dto.Descripcion.Trim().Length < 5)
            return BadRequest(new { error = "Descripcion must be at least 5 characters" });

        string gameName = "Unknown";
        if (dto.GameId.HasValue)
            gameName = GameDefinitions.GetById(dto.GameId.Value)?.Name ?? "Unknown";
        else if (!string.IsNullOrEmpty(dto.GameName))
            gameName = dto.GameName;

        try
        {
            var profile = await _groq.ParseDescripcionAsync(dto.Descripcion.Trim(), gameName);

            // Post-parse: recalculate group sizes using resolved mode teamSize
            var game = dto.GameId.HasValue ? GameDefinitions.GetById(dto.GameId.Value) : null;
            if (game != null)
            {
                string? parsedModeKey = profile.Modo;
                int? modeTeamSize = null;
                if (!string.IsNullOrEmpty(parsedModeKey))
                {
                    var modeDef = game.Modes.FirstOrDefault(m => m.Key.Equals(parsedModeKey, StringComparison.OrdinalIgnoreCase));
                    if (modeDef != null && modeDef.TeamSize > 0)
                        modeTeamSize = modeDef.TeamSize;
                }
                if (!modeTeamSize.HasValue)
                    modeTeamSize = game.Modes.Where(m => m.TeamSize > 0).Max(m => (int?)m.TeamSize);

                if (modeTeamSize.HasValue)
                {
                    var total = profile.TamañoEquipoBuscado;
                    var current = profile.TamañoGrupoActual;

                    if (total.HasValue && total.Value < modeTeamSize.Value && !current.HasValue)
                    {
                        int seeking = total.Value;
                        int realCurrent = modeTeamSize.Value - seeking;
                        if (realCurrent >= 1)
                        {
                            profile = profile with
                            {
                                TamañoEquipoBuscado = modeTeamSize.Value,
                                TamañoGrupoActual = realCurrent
                            };
                        }
                    }
                    else if (total.HasValue && current.HasValue && total.Value != modeTeamSize.Value)
                    {
                        if (current.Value + total.Value == modeTeamSize.Value)
                        {
                            profile = profile with
                            {
                                TamañoEquipoBuscado = modeTeamSize.Value
                            };
                        }
                    }
                }
            }

            _logger.LogInformation("Parse result — Estilo={Estilo} Rango={Rango} TamañoEquipo={Size}",
                profile.Estilo, profile.Rango, profile.TamañoEquipoBuscado);

            var warnings = new List<string>();
            bool isValid = true;

            bool isRanked = profile.Estilo?.Equals("ranked", StringComparison.OrdinalIgnoreCase) == true;
            bool hasRank = !string.IsNullOrWhiteSpace(profile.Rango);
            bool hasSize = profile.TamañoEquipoBuscado.HasValue && profile.TamañoEquipoBuscado.Value >= 2;

            if (isRanked && !hasRank)
            {
                warnings.Add("Para jugar competitivo tenés que especificar tu rango");
                isValid = false;
            }

            if (!hasSize)
            {
                warnings.Add("No especificaste cuántos jugadores buscan en total");
                isValid = false;
            }

            return Ok(new { profile, warnings, isValid });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Parse failed for descripcion: {Desc}", dto.Descripcion);
            return StatusCode(500, new { error = "Groq parse failed" });
        }
    }
}
