namespace AuthBridge.DTOs;

/// <summary>
/// Tells the client which authentication modes are currently available, so the login UI
/// can switch between Basic (email/password) and SSO (Entra ID) flows.
/// </summary>
public class AuthOptionsDto
{
    public bool SsoEnabled { get; set; }
}
