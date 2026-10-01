using AuthBridge.DTOs;

namespace AuthBridge.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);

    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);

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
}
