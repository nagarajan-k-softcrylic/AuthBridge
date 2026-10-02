namespace ScimProvisioning.Api.Entities;

/// <summary>
/// Tracks whether a given AuthBridge user has been SCIM-provisioned into a given downstream
/// application, and when it last synced. One row per (ApplicationId, UserId) pair.
/// </summary>
public class ScimApplicationAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ApplicationId { get; set; }

    public ScimApplication? Application { get; set; }

    /// <summary>AuthBridge user id.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The SCIM resource id assigned by the downstream application on create, used for
    /// subsequent update/disable/delete calls.</summary>
    public string? ScimExternalId { get; set; }

    public bool Provisioned { get; set; }

    public DateTime? ProvisionedAtUtc { get; set; }

    public DateTime? LastSyncUtc { get; set; }

    /// <summary>True while the user's access to this application is active; set false on revoke
    /// (SCIM "disable"), mirroring AuthBridge's UserApplication.IsActive.</summary>
    public bool IsActive { get; set; } = true;
}
