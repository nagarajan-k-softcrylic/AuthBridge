namespace AuthBridge.Services;

/// <summary>
/// Notifies the standalone SCIM Provisioning Server (ScimProvisioning.Api) of application
/// assignment/revocation/profile/role lifecycle events, so it can synchronize the user into (or
/// out of) the corresponding downstream application via SCIM 2.0.
/// </summary>
/// <remarks>
/// Business rule: AuthBridge is the source of truth and NEVER calls SCIM on user creation, login,
/// logout, password change, MFA enable/disable, or refresh-token issuance - only on application
/// assignment, revocation, and (for already-assigned applications) profile/role updates. Failures
/// calling the SCIM server are logged and swallowed so they never block the AuthBridge operation
/// that triggered them (e.g. the assignment itself still succeeds even if the downstream SCIM
/// call fails - ScimProvisioning.Api's own audit log/retry mechanism handles recovery).
/// </remarks>
public interface IScimNotificationService
{
    Task NotifyApplicationAssignedAsync(string applicationCode, string userId, string userName, string? firstName, string? lastName, IReadOnlyList<string> roles);

    Task NotifyApplicationRevokedAsync(string applicationCode, string userId);

    Task NotifyUserProfileUpdatedAsync(string userId, string userName, string? firstName, string? lastName, bool active);

    Task NotifyUserRoleUpdatedAsync(string applicationCode, string userId, IReadOnlyList<string> roles);
}
