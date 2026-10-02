using System.ComponentModel.DataAnnotations;

namespace ScimProvisioning.Api.DTOs;

/// <summary>Request body AuthBridge sends when an application is assigned to a user.</summary>
public class AssignUserRequest
{
    [Required]
    public string ApplicationCode { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string UserName { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public List<string> Roles { get; set; } = new();

    public string? CorrelationId { get; set; }
}

/// <summary>Request body AuthBridge sends when an application is revoked from a user.</summary>
public class RevokeUserRequest
{
    [Required]
    public string ApplicationCode { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public string? CorrelationId { get; set; }
}

/// <summary>Request body AuthBridge sends when a user's profile changes, so it can be propagated
/// to every application the user is currently (actively) assigned to.</summary>
public class UpdateUserProfileRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string UserName { get; set; } = string.Empty;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public bool Active { get; set; } = true;

    public string? CorrelationId { get; set; }
}

/// <summary>Request body AuthBridge sends when a user's roles change for a given application.</summary>
public class UpdateUserRoleRequest
{
    [Required]
    public string ApplicationCode { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = new();

    public string? CorrelationId { get; set; }
}

/// <summary>Uniform result returned by every provisioning endpoint/service method.</summary>
public class ProvisioningResultDto
{
    public bool Success { get; set; }

    public string? ScimExternalId { get; set; }

    public string? Message { get; set; }
}
