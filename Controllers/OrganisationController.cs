using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prismon.Api.DTOs;
using Prismon.Api.Interface;
using Prismon.Api.Services;
using System.Security.Claims;

namespace Prismon.Api.Controllers;

[ApiController]
[Route("devApi/[controller]")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IAppService _appService;

    public OrganizationsController(IAppService appService)
    {
        _appService = appService;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrganization([FromBody] CreateOrganizationRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var developerId = User.FindFirst("DeveloperId")?.Value
            ?? throw new UnauthorizedAccessException("Developer ID not found in token");

        try
        {
            var response = await _appService.CreateOrganizationAsync(request, developerId);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Succeeded = false, Message = ex.Message });
        }
    }

    [HttpPost("skip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SkipOnboarding()
    {
        var developerId = User.FindFirst("DeveloperId")?.Value
            ?? throw new UnauthorizedAccessException("Developer ID not found in token");

        var result = await _appService.CompleteOnboardingAsync(developerId);
        return Ok(new { Succeeded = result, Message = "Onboarding skipped" });
    }
}