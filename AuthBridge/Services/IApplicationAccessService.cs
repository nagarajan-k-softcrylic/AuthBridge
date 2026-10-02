namespace AuthBridge.Services;

/// <summary>
/// Validates whether a user is allowed to launch a given catalog application, based on their
/// active <c>UserApplication</c> assignment. Used by the "launch" endpoint before redirecting
/// the user to the target application (and, in the future, before issuing an SSO token for it).
/// </summary>
public interface IApplicationAccessService
{
    /// <summary>True if the user has an active assignment to the application and the application
    /// itself is active (not disabled/soft-deleted).</summary>
    Task<bool> HasAccessAsync(string userId, Guid applicationId);

    /// <summary>Same check by application code (e.g. "RESUME_AI"), for callers that only know the code.</summary>
    Task<bool> HasAccessByCodeAsync(string userId, string applicationCode);
}
