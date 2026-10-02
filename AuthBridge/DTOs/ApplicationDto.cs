namespace AuthBridge.DTOs;

/// <summary>Catalog application as returned to clients (admin search/list and user-facing views).</summary>
public class ApplicationDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string ApplicationCode { get; set; } = string.Empty;

    public string ApplicationUrl { get; set; } = string.Empty;

    public string? IconUrl { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>True when the current user (if any) has an active assignment to this application.</summary>
    public bool IsAssignedToCurrentUser { get; set; }
}

/// <summary>Request body for registering a new application in the catalog (ApplicationAdmin only).</summary>
public class CreateApplicationDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string ApplicationCode { get; set; } = string.Empty;

    public string ApplicationUrl { get; set; } = string.Empty;

    public string? IconUrl { get; set; }
}

/// <summary>Request body for updating an existing catalog application (ApplicationAdmin only).</summary>
public class UpdateApplicationDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string ApplicationUrl { get; set; } = string.Empty;

    public string? IconUrl { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>Request body for assigning an application to a user (ApplicationAdmin only).</summary>
public class AssignApplicationDto
{
    public string UserId { get; set; } = string.Empty;

    public Guid ApplicationId { get; set; }
}

/// <summary>Request body for assigning an application by the target user's email (ApplicationAdmin
/// only) - used by the Application Search UI, which looks users up by email rather than by raw Id.</summary>
public class AssignApplicationByEmailDto
{
    public string Email { get; set; } = string.Empty;

    public Guid ApplicationId { get; set; }
}

/// <summary>Request body for revoking a user's access to an application (ApplicationAdmin only).</summary>
public class RevokeApplicationDto
{
    public string UserId { get; set; } = string.Empty;

    public Guid ApplicationId { get; set; }
}

/// <summary>A user's assignment to a catalog application, including app metadata for display.</summary>
public class UserApplicationDto
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string? UserEmail { get; set; }

    public Guid ApplicationId { get; set; }

    public string ApplicationName { get; set; } = string.Empty;

    public string ApplicationCode { get; set; } = string.Empty;

    public string ApplicationUrl { get; set; } = string.Empty;

    public string? IconUrl { get; set; }

    public bool IsActive { get; set; }

    public DateTime AssignedAtUtc { get; set; }

    public string? AssignedBy { get; set; }
}

/// <summary>Generic paged result wrapper used by catalog search/list endpoints.</summary>
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}
