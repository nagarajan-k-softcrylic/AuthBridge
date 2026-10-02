namespace AuthBridge.DTOs;

public class AuthResponseDto
{
    public bool Succeeded { get; set; }

    public string? Token { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiresAtUtc { get; set; }

    public string? UserId { get; set; }

    public string? Email { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public bool RequiresMfa { get; set; }

    /// <summary>True when MFA is currently enabled on the account (used to toggle the Enable/Disable MFA UI).</summary>
    public bool MfaEnabled { get; set; }

    /// <summary>Role names assigned to the user (e.g. "ApplicationAdmin"), used by the frontend to
    /// conditionally show admin-only features like the Application Search / catalog management page.</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Raw "remember this device" token, set only by VerifyMfaAsync when the user opted in.
    /// Never sent to the client as JSON - the controller moves it into an httpOnly cookie and
    /// nulls this out before returning the response body (mirrors Token/RefreshToken handling).
    /// </summary>
    public string? TrustedDeviceToken { get; set; }

    public List<string> Errors { get; set; } = new();
}

/// <summary>Request body for completing login when <see cref="AuthResponseDto.RequiresMfa"/> was true.</summary>
public class MfaVerifyRequestDto
{
    public string UserId { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    /// <summary>If true, this device skips the MFA prompt on future logins for 7 days.</summary>
    public bool RememberDevice { get; set; }
}

/// <summary>Request body for confirming MFA setup (enable) with a code from the authenticator app.</summary>
public class MfaEnableRequestDto
{
    public string Code { get; set; } = string.Empty;
}

/// <summary>Shared key and otpauth:// URI needed to add the account to an authenticator app.</summary>
public class MfaSetupDto
{
    public string SharedKey { get; set; } = string.Empty;

    public string AuthenticatorUri { get; set; } = string.Empty;
}
