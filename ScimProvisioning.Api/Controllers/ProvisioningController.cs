using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScimProvisioning.Api.DTOs;
using ScimProvisioning.Api.Services;

namespace ScimProvisioning.Api.Controllers;

/// <summary>
/// Integration surface AuthBridge calls to trigger SCIM provisioning. AuthBridge remains the
/// source of truth for users/roles/application assignments; this controller is the ONLY entry
/// point that fans those lifecycle events out to downstream applications via SCIM. Secured with
/// a service-to-service API key (see Security/ApiKeyAuthenticationHandler) in addition to the
/// standard Bearer scheme, since callers are backend services, not end users.
/// </summary>
[ApiController]
[Route("api/provisioning")]
[Authorize(AuthenticationSchemes = "Bearer,ApiKey")]
public class ProvisioningController : ControllerBase
{
    private readonly IScimProvisioningService _provisioningService;
    private readonly ILogger<ProvisioningController> _logger;

    public ProvisioningController(IScimProvisioningService provisioningService, ILogger<ProvisioningController> logger)
    {
        _provisioningService = provisioningService;
        _logger = logger;
    }

    /// <summary>Triggered when AuthBridge assigns an application to a user. Creates the user
    /// (and roles/groups, if any) in the target application via SCIM.</summary>
    [HttpPost("assign-user")]
    public async Task<ActionResult<ProvisioningResultDto>> AssignUser([FromBody] AssignUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _provisioningService.ProvisionApplicationAsync(request);
        return result.Success ? Ok(result) : UnprocessableEntity(result);
    }

    /// <summary>Triggered when AuthBridge revokes an application from a user. Disables (does not
    /// delete) the user in that application only; the user remains active elsewhere.</summary>
    [HttpPost("revoke-user")]
    public async Task<ActionResult<ProvisioningResultDto>> RevokeUser([FromBody] RevokeUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _provisioningService.DisableUserAsync(request);
        return result.Success ? Ok(result) : UnprocessableEntity(result);
    }

    /// <summary>Triggered when a user's profile is updated in AuthBridge. Propagates the change
    /// to every application the user is currently actively assigned to.</summary>
    [HttpPost("update-user")]
    public async Task<ActionResult<ProvisioningResultDto>> UpdateUser([FromBody] UpdateUserProfileRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _provisioningService.UpdateUserAsync(request);
        return result.Success ? Ok(result) : UnprocessableEntity(result);
    }

    /// <summary>Triggered when a user's role(s) change for a specific application. Updates
    /// SCIM group membership in that application only.</summary>
    [HttpPost("update-role")]
    public async Task<ActionResult<ProvisioningResultDto>> UpdateRole([FromBody] UpdateUserRoleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _provisioningService.AssignGroupAsync(request);
        return result.Success ? Ok(result) : UnprocessableEntity(result);
    }

    /// <summary>Re-attempts every currently failed provisioning log entry (ScimAdmin only).</summary>
    [Authorize(AuthenticationSchemes = "Bearer", Roles = "ScimAdmin")]
    [HttpPost("retry-failed")]
    public async Task<ActionResult<object>> RetryFailed()
    {
        var count = await _provisioningService.RetryFailedProvisioningAsync();
        return Ok(new { retried = count });
    }
}
