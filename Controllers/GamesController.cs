using Microsoft.AspNetCore.Mvc;
using WannaFill.API.GameConfig;

namespace WannaFill.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(GameDefinitions.All);
    }
}
