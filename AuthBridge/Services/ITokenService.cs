using AuthBridge.Entities;

namespace AuthBridge.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(ApplicationUser user, IList<string> roles);

    /// <summary>Generates a cryptographically random raw refresh token (returned to the client once).</summary>
    string GenerateRefreshToken();

    /// <summary>Hashes a raw refresh token for storage/lookup; the raw value is never persisted.</summary>
    string HashRefreshToken(string rawToken);
}
