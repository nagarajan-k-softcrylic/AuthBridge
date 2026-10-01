namespace AuthBridge.Configuration;

/// <summary>
/// Options bound from the "Jwt" section of appsettings.json. Used by the Basic Authentication
/// (username/password) login flow to issue short-lived access tokens for the Angular UI.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "AuthBridge";

    public string Audience { get; set; } = "AuthBridge.Clients";

    /// <summary>Symmetric signing key. TODO: move to a secret store (Key Vault/User Secrets) before production.</summary>
    public string Key { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>How long a refresh token remains valid before the user must log in again.</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
