namespace AuthBridge.Entities;

/// <summary>
/// Remembers that a browser/device has already completed an MFA challenge for a user, so
/// subsequent logins from the same device can skip the TOTP prompt until this record expires
/// (7 days - see AuthService.MfaTrustedDeviceDays). Only a SHA-256 hash of the raw token is
/// persisted (never the raw value), mirroring <see cref="RefreshToken"/>.
/// </summary>
public class MfaTrustedDevice
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }

    public bool IsActive => DateTime.UtcNow < ExpiresAtUtc;
}
