using Microsoft.AspNetCore.Mvc;
using WannaFill.API.DTOs;
using WannaFill.API.Services;

namespace WannaFill.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchmakingController : ControllerBase
{
    private readonly IMatchmakingService _matchmaking;
    private readonly ILogger<MatchmakingController> _logger;

    public MatchmakingController(IMatchmakingService matchmaking, ILogger<MatchmakingController> logger)
    {
        _matchmaking = matchmaking;
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
}
