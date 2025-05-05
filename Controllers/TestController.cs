using Microsoft.AspNetCore.Mvc;
using Prismon.Api.Models;

namespace Prismon.Api.Controllers;

[ApiController]
[Route("devApi/[controller]")]
public class TestController : ControllerBase
{
    [HttpGet("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public IActionResult TestAuth()
    {
        var app = HttpContext.Items["App"] as App;
        if (app == null)
        {
            return BadRequest(new { Message = "App context not found" });
        }
        return Ok(new { Message = "API Key authenticated", AppId = app.Id, AppName = app.Name });
    }
}