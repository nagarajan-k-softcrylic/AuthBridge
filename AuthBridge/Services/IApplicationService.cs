using AuthBridge.DTOs;

namespace AuthBridge.Services;

/// <summary>
/// Manages the Application Catalog: registering/updating applications, searching/listing them,
/// and assigning/revoking user access. Used by <c>ApplicationsController</c> for both
/// ApplicationAdmin-only catalog management and user-facing "my applications" queries.
/// </summary>
public interface IApplicationService
{
    /// <summary>Paged, optionally filtered search over active (non-deleted) catalog applications.
    /// When <paramref name="currentUserId"/> is supplied, flags each result with whether that
    /// user currently has an active assignment to it.</summary>
    Task<PagedResult<ApplicationDto>> SearchAsync(string? searchTerm, int page, int pageSize, string? currentUserId = null);

    Task<ApplicationDto?> GetByIdAsync(Guid id);

    Task<ApplicationDto> CreateAsync(CreateApplicationDto request, string createdBy);

    Task<ApplicationDto?> UpdateAsync(Guid id, UpdateApplicationDto request, string updatedBy);

    /// <summary>Soft-deletes the application (removed from catalog; existing assignment history is preserved).</summary>
    Task<bool> DeleteAsync(Guid id, string deletedBy);

    /// <summary>Grants a user access to an application. Re-activates a previously revoked assignment
    /// if one exists, rather than creating a duplicate row.</summary>
    Task<UserApplicationDto> AssignAsync(AssignApplicationDto request, string assignedBy);

    /// <summary>Same as <see cref="AssignAsync"/>, but resolves the target user by email instead of Id.</summary>
    Task<UserApplicationDto> AssignByEmailAsync(AssignApplicationByEmailDto request, string assignedBy);

    /// <summary>Revokes a user's access to an application (soft: marks the assignment inactive).</summary>
    Task<bool> RevokeAsync(RevokeApplicationDto request, string revokedBy);

    /// <summary>Active applications assigned to the given user, for the launch portal.</summary>
    Task<List<UserApplicationDto>> GetMyApplicationsAsync(string userId);

    /// <summary>All active assignments for an application (ApplicationAdmin view of who has access).</summary>
    Task<List<UserApplicationDto>> GetAssignmentsForApplicationAsync(Guid applicationId);
}
