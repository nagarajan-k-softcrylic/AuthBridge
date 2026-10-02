namespace ScimProvisioning.Api.Entities;

/// <summary>
/// A downstream application registered to receive SCIM 2.0 provisioning calls from this server
/// (e.g. "ResumeScreener AI", "Report Generator"). Mirrors the AuthBridge Application Catalog
/// entry via <see cref="ApplicationCode"/>, but is owned independently by this service.
/// </summary>
public class ScimApplication
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>Stable code matching AuthBridge's Application.ApplicationCode (e.g. "RESUME_AI").</summary>
    public string ApplicationCode { get; set; } = string.Empty;

    /// <summary>Base URL of the downstream application's SCIM 2.0 endpoint (e.g. https://app/scim/v2).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Bearer token used to authenticate outbound SCIM calls to this application.
    /// Stored encrypted at rest (see <c>ISecretProtector</c>) - never logged or returned by the API.</summary>
    public string AccessToken { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ScimApplicationAssignment> Assignments { get; set; } = new List<ScimApplicationAssignment>();
}
