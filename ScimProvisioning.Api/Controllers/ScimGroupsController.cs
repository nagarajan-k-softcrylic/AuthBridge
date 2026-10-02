using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScimProvisioning.Api.Scim;

namespace ScimProvisioning.Api.Controllers;

/// <summary>
/// SCIM 2.0 "Groups" endpoint (RFC 7644 §3.2). AuthBridge roles (ApplicationAdmin, Recruiter,
/// ReportViewer, ...) are represented as SCIM groups; membership changes are driven internally
/// via <see cref="Services.IScimProvisioningService.AssignGroupAsync"/> /
/// <see cref="Services.IScimProvisioningService.RemoveGroupAsync"/>. This controller is an
/// in-memory stub satisfying the RFC 7644 surface for external SCIM clients/tests; persistent
/// group storage can be added alongside ScimApplicationAssignment if/when needed.
/// </summary>
[ApiController]
[Route("scim/v2/Groups")]
[Authorize(AuthenticationSchemes = "Bearer")]
[Produces("application/scim+json")]
public class ScimGroupsController : ControllerBase
{
    private static readonly List<ScimGroup> Groups = new()
    {
        new ScimGroup { Id = "1", DisplayName = "ApplicationAdmin" },
        new ScimGroup { Id = "2", DisplayName = "Recruiter" },
        new ScimGroup { Id = "3", DisplayName = "ReportViewer" },
    };

    [HttpGet]
    public ActionResult<ScimListResponse<ScimGroup>> Get() => Ok(new ScimListResponse<ScimGroup>
    {
        TotalResults = Groups.Count,
        ItemsPerPage = Groups.Count,
        Resources = Groups,
    });

    [HttpPost]
    public ActionResult<ScimGroup> Create([FromBody] ScimGroup group)
    {
        group.Id = Guid.NewGuid().ToString();
        Groups.Add(group);
        return CreatedAtAction(nameof(Get), group);
    }

    [HttpPatch("{id}")]
    public ActionResult<ScimGroup> Patch(string id, [FromBody] ScimPatchRequest patch)
    {
        var group = Groups.FirstOrDefault(g => g.Id == id);
        if (group is null)
        {
            return NotFound(new ScimError { Status = "404", Detail = "Group not found." });
        }

        foreach (var op in patch.Operations.Where(o => o.Path == "members"))
        {
            if (op.Op == "add" && op.Value is not null)
            {
                group.Members.Add(new ScimMember { Value = op.Value.ToString() ?? string.Empty });
            }
            else if (op.Op == "remove")
            {
                group.Members.RemoveAll(m => m.Value == op.Value?.ToString());
            }
        }

        return Ok(group);
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        var group = Groups.FirstOrDefault(g => g.Id == id);
        if (group is null)
        {
            return NotFound(new ScimError { Status = "404", Detail = "Group not found." });
        }

        Groups.Remove(group);
        return NoContent();
    }
}
