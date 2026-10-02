using AuthBridge.DTOs;

namespace AuthBridge.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);

    /// <summary>
    /// Authenticates with email/password. If the account has MFA enabled, returns
    /// <c>RequiresMfa = true</c> without issuing tokens - unless <paramref name="trustedDeviceToken"/>
    /// matches an active "remember this device" record for the user (see VerifyMfaAsync), in which
    /// case the second factor is skipped and tokens are issued immediately.
    /// </summary>
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, string? trustedDeviceToken = null);

    /// <summary>
    /// Exchanges a valid, unexpired refresh token for a new access token + rotated refresh
    /// token (the old refresh token is revoked so it can't be replayed).
    /// </summary>
    Task<AuthResponseDto> RefreshAsync(string rawRefreshToken);

    /// <summary>Revokes a refresh token (used on logout) so it can no longer be exchanged.</summary>
    Task RevokeRefreshTokenAsync(string rawRefreshToken);

    /// <summary>
    /// Issues the same JWT access/refresh token pair as LoginAsync, but for a user who has
    /// already been authenticated via the standalone OIDC flow (Cookies scheme) rather than a
    /// password check. Used by the OIDC callback to bridge the "Cookies"-authenticated identity
    /// back into the SPA's existing JWT-based session model.
    /// </summary>
    Task<AuthResponseDto> IssueTokensForOidcUserAsync(string userId);

    /// <summary>
    /// Issues the same JWT access/refresh token pair as LoginAsync, for a user authenticated by
    /// an external IdP (Microsoft Entra ID) rather than a local password. Since the external
    /// identity's subject id does not correspond to a local AspNetUsers row, the user is matched
    /// (or auto-provisioned on first login) by email instead.
    /// </summary>
    Task<AuthResponseDto> IssueTokensForExternalUserAsync(string email, string? firstName, string? lastName);

    /// <summary>
    /// Generates (or reuses) the TOTP shared key for the given user and returns it along with
    /// the otpauth:// URI for scanning into an authenticator app. Does not enable MFA yet -
    /// that happens once the user confirms a generated code via <see cref="EnableMfaAsync"/>.
    /// </summary>
    Task<MfaSetupDto> GetMfaSetupAsync(string userId);

    /// <summary>Confirms MFA setup: validates the submitted TOTP code and, if valid, turns on MFA for the user.</summary>
    Task<AuthResponseDto> EnableMfaAsync(string userId, string code);

    /// <summary>
    /// Turns off MFA for the user (no code re-verification required). Also revokes any
    /// "remember this device" records, since trusting a device only makes sense while MFA is on.
    /// </summary>
    Task<AuthResponseDto> DisableMfaAsync(string userId);

    /// <summary>
    /// Completes a login that was paused with <see cref="AuthResponseDto.RequiresMfa"/>: validates
    /// the submitted TOTP code and, if valid, issues the normal JWT access/refresh token pair. When
    /// <paramref name="rememberDevice"/> is true, also issues a 7-day "remember this device" token
    /// (returned via <see cref="AuthResponseDto.TrustedDeviceToken"/>) that lets LoginAsync skip the
    /// MFA prompt on this device until it expires.
    /// </summary>
    Task<AuthResponseDto> VerifyMfaAsync(string userId, string code, bool rememberDevice = false);
}
