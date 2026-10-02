namespace AuthBridge.Entities;

/// <summary>
/// A registered downstream application in the AuthBridge Application Catalog (e.g. "ResumeScreener
/// AI", "Report Generator"). AuthBridge only stores metadata and access-control records for these
/// applications - it does not host or implement them. Soft-deleted via <see cref="IsDeleted"/>
/// rather than hard-deleted, so assignment/audit history is preserved.
/// </summary>
public class Application
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Stable machine-readable identifier (e.g. "RESUME_AI") used by future SSO token
    /// issuance/validation to identify which application a token/grant is for.</summary>
    public string ApplicationCode { get; set; } = string.Empty;

    /// <summary>Base URL the Application Launch Portal redirects/opens when a user launches this app.</summary>
    public string ApplicationUrl { get; set; } = string.Empty;

    public string? IconUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public string? UpdatedBy { get; set; }

    /// <summary>Soft-delete flag; deleted applications are excluded from catalog/search/assignment.</summary>
    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public string? DeletedBy { get; set; }

    public ICollection<UserApplication> UserApplications { get; set; } = new List<UserApplication>();
}
