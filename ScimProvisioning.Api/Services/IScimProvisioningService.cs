using ScimProvisioning.Api.DTOs;

namespace ScimProvisioning.Api.Services;

/// <summary>
/// Core provisioning engine: translates AuthBridge application-assignment lifecycle events into
/// outbound SCIM 2.0 calls against the target application's SCIM endpoint, with audit logging
/// and retry support. Triggered ONLY by application assignment/revocation and, for already
/// assigned applications, by profile/role updates - never by user creation, login, logout,
/// password change, MFA, or refresh-token events.
/// </summary>
public interface IScimProvisioningService
{
    /// <summary>Creates (or re-activates) the user in the target application via SCIM and records
    /// the assignment. Called when an application is assigned to a user.</summary>
    Task<ProvisioningResultDto> ProvisionApplicationAsync(AssignUserRequest request);

    Task<ProvisioningResultDto> CreateUserAsync(AssignUserRequest request);

    Task<ProvisioningResultDto> UpdateUserAsync(UpdateUserProfileRequest request);

    /// <summary>Disables (not deletes) the user in the target application. Called on revocation -
    /// the user remains active in every other assigned application.</summary>
    Task<ProvisioningResultDto> DisableUserAsync(RevokeUserRequest request);

    Task<ProvisioningResultDto> DeleteUserAsync(RevokeUserRequest request);

    Task<ProvisioningResultDto> AssignGroupAsync(UpdateUserRoleRequest request);

    Task<ProvisioningResultDto> RemoveGroupAsync(UpdateUserRoleRequest request);

    /// <summary>Re-attempts every log row currently in "Failed" status.</summary>
    Task<int> RetryFailedProvisioningAsync();
}
