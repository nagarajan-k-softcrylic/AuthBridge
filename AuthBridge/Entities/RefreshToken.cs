namespace AuthBridge.Entities;

/// <summary>
/// A long-lived refresh token used to silently obtain new short-lived JWT access tokens
/// without requiring the user to re-enter credentials. Only a SHA-256 hash of the raw
/// token is persisted (never the raw value) so a database leak alone cannot be used to
/// impersonate a user. Rotated (one-time use) on every refresh to limit replay windows.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the token has been used (rotated) or explicitly revoked (logout).</summary>
    public DateTime? RevokedAtUtc { get; set; }

    public ApplicationUser? User { get; set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}
