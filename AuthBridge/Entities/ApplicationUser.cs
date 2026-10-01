using Microsoft.AspNetCore.Identity;

namespace AuthBridge.Entities;

/// <summary>
/// Application user backing the ASP.NET Core Identity authentication/registration tables
/// (AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetUserLogins, AspNetUserTokens).
/// Extend with AuthBridge-specific profile fields as needed.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    /// <summary>
    /// True when the user must complete MFA verification as part of sign-in.
    /// </summary>
    public bool MfaEnabled { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
