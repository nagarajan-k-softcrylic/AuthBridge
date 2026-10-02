using System.Security.Claims;
using AuthBridge.DTOs;
using AuthBridge.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthBridge.Controllers;

/// <summary>
/// Application Catalog and Access Management Portal endpoints. Catalog management (create/update/
/// delete/assign/revoke) is restricted to the "ApplicationAdmin" role; "my-applications" and
/// "launch" are available to any authenticated user for their own assigned applications.
/// </summary>
[ApiController]
[Route("api/applications")]
[Authorize(AuthenticationSchemes = "Bearer")]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _applicationService;
    private readonly IApplicationAccessService _applicationAccessService;

    public ApplicationsController(
        IApplicationService applicationService,
        IApplicationAccessService applicationAccessService)
    {
        _applicationService = applicationService;
        _applicationAccessService = applicationAccessService;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
    private string CurrentUserEmail => User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? CurrentUserId ?? "unknown";

    /// <summary>Paged, optionally filtered search over the application catalog (ApplicationAdmin only).</summary>
    [Authorize(Roles = "ApplicationAdmin")]
    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<ApplicationDto>>> Search(
        [FromQuery] string? searchTerm, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Ok(await _applicationService.SearchAsync(searchTerm, page, pageSize, CurrentUserId));
    }

    [Authorize(Roles = "ApplicationAdmin")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApplicationDto>> GetById(Guid id)
    {
        var app = await _applicationService.GetByIdAsync(id);
        return app is null ? NotFound() : Ok(app);
    }

    [Authorize(Roles = "ApplicationAdmin")]
    [HttpPost]
    public async Task<ActionResult<ApplicationDto>> Create([FromBody] CreateApplicationDto request)
    {
        try
        {
            var app = await _applicationService.CreateAsync(request, CurrentUserEmail);
            return CreatedAtAction(nameof(GetById), new { id = app.Id }, app);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "ApplicationAdmin")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApplicationDto>> Update(Guid id, [FromBody] UpdateApplicationDto request)
    {
        var app = await _applicationService.UpdateAsync(id, request, CurrentUserEmail);
        return app is null ? NotFound() : Ok(app);
    }

    [Authorize(Roles = "ApplicationAdmin")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _applicationService.DeleteAsync(id, CurrentUserEmail);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Grants a user access to an application. Prevents duplicate assignment (idempotent re-activation).</summary>
    [Authorize(Roles = "ApplicationAdmin")]
    [HttpPost("assign")]
    public async Task<ActionResult<UserApplicationDto>> Assign([FromBody] AssignApplicationDto request)
    {
        try
        {
            return Ok(await _applicationService.AssignAsync(request, CurrentUserEmail));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Assigns an application to a user identified by email (Application Search UI "Assign
    /// User" action, where the admin only knows the user's email, not their raw Id).</summary>
    [Authorize(Roles = "ApplicationAdmin")]
    [HttpPost("assign-by-email")]
    public async Task<ActionResult<UserApplicationDto>> AssignByEmail([FromBody] AssignApplicationByEmailDto request)
    {
        try
        {
            return Ok(await _applicationService.AssignByEmailAsync(request, CurrentUserEmail));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "ApplicationAdmin")]
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke([FromBody] RevokeApplicationDto request)
    {
        var revoked = await _applicationService.RevokeAsync(request, CurrentUserEmail);
        return revoked ? Ok() : NotFound();
    }

    [Authorize(Roles = "ApplicationAdmin")]
    [HttpGet("{id:guid}/assignments")]
    public async Task<ActionResult<List<UserApplicationDto>>> GetAssignments(Guid id)
    {
        return Ok(await _applicationService.GetAssignmentsForApplicationAsync(id));
    }

    /// <summary>Applications assigned to the currently signed-in user (My Applications launch portal).</summary>
    [HttpGet("my-applications")]
    public async Task<ActionResult<List<UserApplicationDto>>> MyApplications()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        return Ok(await _applicationService.GetMyApplicationsAsync(CurrentUserId));
    }

    /// <summary>
    /// Validates the current user's access to an application before launching it. Full SSO token
    /// issuance (<see cref="IApplicationTokenService"/>) is not implemented yet - this only confirms
    /// the user is allowed to proceed and returns the application's launch URL.
    /// </summary>
    [HttpGet("{id:guid}/launch")]
    public async Task<IActionResult> Launch(Guid id)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        var hasAccess = await _applicationAccessService.HasAccessAsync(CurrentUserId, id);
        if (!hasAccess)
        {
            return Forbid();
        }

        var app = await _applicationService.GetByIdAsync(id);
        return app is null ? NotFound() : Ok(new { launchUrl = app.ApplicationUrl });
    }
}
