namespace AuthBridge.Entities;

/// <summary>
/// Grants a user access to a catalog <see cref="Application"/>. Created when an
/// <c>ApplicationAdmin</c> assigns an application to a user; revoked (soft-deleted via
/// <see cref="IsActive"/> = false) rather than hard-deleted, so the assignment/revocation
/// history is preserved for audit purposes.
/// </summary>
public class UserApplication
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public Guid ApplicationId { get; set; }

    public Application? Application { get; set; }

    /// <summary>True while the assignment is active; set to false on revocation (kept for audit history).</summary>
    public bool IsActive { get; set; } = true;

    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;

    public string? AssignedBy { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedBy { get; set; }
}
