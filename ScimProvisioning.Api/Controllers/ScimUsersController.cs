using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScimProvisioning.Api.Data;
using ScimProvisioning.Api.Scim;
using Microsoft.EntityFrameworkCore;

namespace ScimProvisioning.Api.Controllers;

/// <summary>
/// SCIM 2.0 "Users" endpoint (RFC 7644 §3.2) exposed BY this server, backed by the local
/// ScimApplicationAssignment projection. Allows a downstream application (or an external SCIM
/// client) to query/manage the provisioning records this server owns, independent of the
/// internal /api/provisioning/* endpoints AuthBridge calls to trigger provisioning.
/// </summary>
[ApiController]
[Route("scim/v2/Users")]
[Authorize(AuthenticationSchemes = "Bearer")]
[Produces("application/scim+json")]
public class ScimUsersController : ControllerBase
{
    private readonly ScimDbContext _db;

    public ScimUsersController(ScimDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<ScimListResponse<ScimUser>>> Get([FromQuery] string? filter, [FromQuery] int startIndex = 1, [FromQuery] int count = 20)
    {
        var query = _db.ScimApplicationAssignments.Include(a => a.Application).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter) && filter.Contains("userName", StringComparison.OrdinalIgnoreCase))
        {
            var value = filter.Split("\"").ElementAtOrDefault(1);
            if (!string.IsNullOrWhiteSpace(value))
            {
                query = query.Where(a => a.UserId == value);
            }
        }

        var total = await query.CountAsync();
        var page = await query.Skip(startIndex - 1).Take(count).ToListAsync();

        return Ok(new ScimListResponse<ScimUser>
        {
            TotalResults = total,
            StartIndex = startIndex,
            ItemsPerPage = page.Count,
            Resources = page.Select(ToScimUser).ToList(),
        });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ScimUser>> GetById(string id)
    {
        var assignment = await _db.ScimApplicationAssignments.Include(a => a.Application)
            .FirstOrDefaultAsync(a => a.ScimExternalId == id);

        return assignment is null
            ? NotFound(new ScimError { Status = "404", Detail = "User not found." })
            : Ok(ToScimUser(assignment));
    }

    /// <summary>Registers a SCIM user resource directly against this server's local store
    /// (used by external SCIM clients; AuthBridge instead calls /api/provisioning/assign-user).</summary>
    [HttpPost]
    public async Task<ActionResult<ScimUser>> Create([FromBody] ScimUser user, [FromQuery] Guid applicationId)
    {
        var assignment = new Entities.ScimApplicationAssignment
        {
            ApplicationId = applicationId,
            UserId = user.UserName,
            ScimExternalId = Guid.NewGuid().ToString(),
            Provisioned = true,
            ProvisionedAtUtc = DateTime.UtcNow,
            LastSyncUtc = DateTime.UtcNow,
            IsActive = user.Active,
        };

        await _db.ScimApplicationAssignments.AddAsync(assignment);
        await _db.SaveChangesAsync();

        var created = ToScimUser(assignment);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ScimUser>> Replace(string id, [FromBody] ScimUser user)
    {
        var assignment = await _db.ScimApplicationAssignments.FirstOrDefaultAsync(a => a.ScimExternalId == id);
        if (assignment is null)
        {
            return NotFound(new ScimError { Status = "404", Detail = "User not found." });
        }

        assignment.IsActive = user.Active;
        assignment.LastSyncUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ToScimUser(assignment));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<ScimUser>> Patch(string id, [FromBody] ScimPatchRequest patch)
    {
        var assignment = await _db.ScimApplicationAssignments.FirstOrDefaultAsync(a => a.ScimExternalId == id);
        if (assignment is null)
        {
            return NotFound(new ScimError { Status = "404", Detail = "User not found." });
        }

        var activeOp = patch.Operations.FirstOrDefault(o => o.Path == "active");
        if (activeOp is not null && bool.TryParse(activeOp.Value?.ToString(), out var active))
        {
            assignment.IsActive = active;
        }

        assignment.LastSyncUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(ToScimUser(assignment));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var assignment = await _db.ScimApplicationAssignments.FirstOrDefaultAsync(a => a.ScimExternalId == id);
        if (assignment is null)
        {
            return NotFound(new ScimError { Status = "404", Detail = "User not found." });
        }

        _db.ScimApplicationAssignments.Remove(assignment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static ScimUser ToScimUser(Entities.ScimApplicationAssignment a) => new()
    {
        Id = a.ScimExternalId,
        UserName = a.UserId,
        Active = a.IsActive,
        Meta = new ScimMeta { Created = a.ProvisionedAtUtc, LastModified = a.LastSyncUtc },
    };
}
