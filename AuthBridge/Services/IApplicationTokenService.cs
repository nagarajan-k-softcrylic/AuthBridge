namespace AuthBridge.Services;

/// <summary>
/// Placeholder for future SSO Gateway support: issuing a short-lived, application-scoped token
/// that a downstream application (e.g. ResumeScreener AI, Report Generator) can validate to trust
/// AuthBridge's authentication decision, instead of each application implementing its own login.
/// Not implemented yet - no downstream application exists to consume it. Intentionally left as an
/// interface only so the contract is settled without building speculative token-issuance logic.
/// </summary>
public interface IApplicationTokenService
{
    /// <summary>
    /// Will issue an application-scoped SSO token for an already-authenticated, access-checked user
    /// (see <see cref="IApplicationAccessService.HasAccessAsync"/>), once a target application exists
    /// to validate it.
    /// </summary>
    Task<string> IssueApplicationTokenAsync(string userId, int applicationId);
}
