using Microsoft.AspNetCore.Mvc;
using Prismon.Api.Models;
using Prismon.Api.Services;

namespace Prismon.Api.Controllers;

[ApiController]
[Route("devApi/[controller]")]
public class DeployController : ControllerBase
{
    private readonly IDeploymentService _deploymentService;

    public DeployController(IDeploymentService deploymentService)
    {
        _deploymentService = deploymentService;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Deploy()
    {
        var app = HttpContext.Items["App"] as App;
        if (app == null)
        {
            return BadRequest(new { Message = "App context not found" });
        }

        var response = await _deploymentService.DeployDAppAsync(app);
        if (!response.Succeeded)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("real")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeployReal()
    {
        var app = HttpContext.Items["App"] as App;
        if (app == null)
        {
            return BadRequest(new { Message = "App context not found" });
        }

        var response = await _deploymentService.DeployDAppRealAsync(app);
        if (!response.Succeeded)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}