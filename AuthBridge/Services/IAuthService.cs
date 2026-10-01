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
}
