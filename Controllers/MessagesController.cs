using Microsoft.AspNetCore.Mvc;
using WannaFill.API.DTOs;
using WannaFill.API.Services;

namespace WannaFill.API.Controllers;

[ApiController]
[Route("api/matchmaking/match/{matchGroupId:guid}/messages")]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messages;

    public MessagesController(IMessageService messages)
    {
        _messages = messages;
    }

    /// <summary>GET — Retrieve all messages for a match.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMessages(Guid matchGroupId)
    {
        var messages = await _messages.GetMessagesAsync(matchGroupId);
        return Ok(messages);
    }

    /// <summary>POST — Send a message to a match chat.</summary>
    [HttpPost]
    public async Task<IActionResult> SendMessage(Guid matchGroupId, [FromBody] SendMessageDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _messages.SendMessageAsync(matchGroupId, dto);
        return result == null
            ? Forbid()
            : Ok(result);
    }
}
