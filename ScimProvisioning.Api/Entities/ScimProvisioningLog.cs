namespace ScimProvisioning.Api.Entities;

public enum ScimOperationType
{
    CreateUser,
    UpdateUser,
    DisableUser,
    DeleteUser,
    AssignGroup,
    RemoveGroup,
}

public enum ScimProvisioningStatus
{
    Pending,
    Success,
    Failed,
}

/// <summary>
/// Audit record of a single outbound SCIM operation to a downstream application. Written for
/// every attempt (success or failure) so provisioning history is fully traceable, and failed
/// rows are the source for <c>RetryFailedProvisioningAsync</c>.
/// </summary>
public class ScimProvisioningLog
{
    public long Id { get; set; }

    public Guid ApplicationId { get; set; }

    public ScimApplication? Application { get; set; }

    /// <summary>AuthBridge user id (not a local identity) - this server never owns user accounts.</summary>
    public string UserId { get; set; } = string.Empty;

    public ScimOperationType OperationType { get; set; }

    public string? RequestPayload { get; set; }

    public string? ResponsePayload { get; set; }

    public ScimProvisioningStatus Status { get; set; } = ScimProvisioningStatus.Pending;

    public string? ErrorMessage { get; set; }

    /// <summary>Correlates this log row to the inbound AuthBridge request for end-to-end tracing.</summary>
    public string? CorrelationId { get; set; }

    public int RetryCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
