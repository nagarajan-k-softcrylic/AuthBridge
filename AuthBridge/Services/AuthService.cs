using System.Text;
using System.Text.Encodings.Web;
using AuthBridge.Configuration;
using AuthBridge.Data;
using AuthBridge.DTOs;
using AuthBridge.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthBridge.Services;

/// <summary>
/// Handles the Basic Authentication (username/password) registration and login flow backed by
/// ASP.NET Core Identity. Entra ID SSO and SAML flows are handled separately via their own
/// authentication handlers/controllers.
/// </summary>
public class AuthService : IAuthService
{
    public const string DefaultRole = "User";
    private const string MfaIssuer = "AuthBridge";
    private const int MfaTrustedDeviceDays = 7;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ITokenService _tokenService;
    private readonly ApplicationDbContext _dbContext;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        ITokenService tokenService,
        ApplicationDbContext dbContext,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
        _dbContext = dbContext;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            return new AuthResponseDto
            {
                Succeeded = false,
                Errors = { "An account with this email already exists." },
            };
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return new AuthResponseDto
            {
                Succeeded = false,
                Errors = createResult.Errors.Select(e => e.Description).ToList(),
            };
        }

        if (!await _roleManager.RoleExistsAsync(DefaultRole))
        {
            await _roleManager.CreateAsync(new IdentityRole(DefaultRole));
        }

        await _userManager.AddToRoleAsync(user, DefaultRole);

        _logger.LogInformation("New user registered: {Email}", user.Email);

        // Establishes the "Identity.Application" cookie alongside the JWT so the interactive
        // OIDC/IdentityServer flow (which challenges that cookie scheme) recognizes the user as
        // already signed in - without this, /connect/authorize keeps redirecting back to login
        // even after a successful Basic Authentication sign-in.
        await _signInManager.SignInAsync(user, isPersistent: true);
        await SignInToIdentityServerCookieSchemeAsync(user);

        return await BuildSuccessResponseAsync(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, string? trustedDeviceToken = null)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return new AuthResponseDto
            {
                Succeeded = false,
                Errors = { "Invalid email or password." },
            };
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return new AuthResponseDto { Succeeded = false, Errors = { "Account locked due to multiple failed attempts. Try again later." } };
        }

        if (!result.Succeeded)
        {
            return new AuthResponseDto { Succeeded = false, Errors = { "Invalid email or password." } };
        }

        if (user.MfaEnabled && !await IsTrustedDeviceAsync(user.Id, trustedDeviceToken))
        {
            // Password check passed, but the account requires a second factor. No tokens are
            // issued yet - the client must call POST /api/auth/mfa/verify with a valid TOTP code
            // (see VerifyMfaAsync) before a session is established.
            return new AuthResponseDto { Succeeded = true, RequiresMfa = true, UserId = user.Id, Email = user.Email };
        }

        // Establishes the "Identity.Application" cookie alongside the JWT so the interactive
        // OIDC/IdentityServer flow (which challenges that cookie scheme) recognizes the user as
        // already signed in - without this, /connect/authorize keeps redirecting back to login
        // even after a successful Basic Authentication sign-in.
        await _signInManager.SignInAsync(user, isPersistent: true);
        await SignInToIdentityServerCookieSchemeAsync(user);

        return await BuildSuccessResponseAsync(user);
    }

    public async Task<AuthResponseDto> RefreshAsync(string rawRefreshToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return new AuthResponseDto { Succeeded = false, Errors = { "Refresh token is missing." } };
        }

        var tokenHash = _tokenService.HashRefreshToken(rawRefreshToken);
        var existing = await _dbContext.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (existing is null || !existing.IsActive || existing.User is null)
        {
            return new AuthResponseDto { Succeeded = false, Errors = { "Refresh token is invalid or expired." } };
        }

        // Rotation: the used token is revoked immediately so it cannot be replayed even if
        // intercepted; a brand new refresh token is issued alongside the new access token.
        existing.RevokedAtUtc = DateTime.UtcNow;

        // BuildSuccessResponseAsync persists the new refresh token and, in the same
        // SaveChangesAsync call, flushes the revocation above (single DbContext instance).
        return await BuildSuccessResponseAsync(existing.User);
    }

    public async Task RevokeRefreshTokenAsync(string rawRefreshToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return;
        }

        var tokenHash = _tokenService.HashRefreshToken(rawRefreshToken);
        var existing = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (existing is not null && existing.RevokedAtUtc is null)
        {
            existing.RevokedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<AuthResponseDto> IssueTokensForOidcUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new AuthResponseDto { Succeeded = false, Errors = { "User not found." } };
        }

        return await BuildSuccessResponseAsync(user);
    }

    public async Task<AuthResponseDto> IssueTokensForExternalUserAsync(string email, string? firstName, string? lastName)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return new AuthResponseDto { Succeeded = false, Errors = { "The external identity did not provide an email address." } };
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName ?? string.Empty,
                LastName = lastName ?? string.Empty,
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                return new AuthResponseDto
                {
                    Succeeded = false,
                    Errors = createResult.Errors.Select(e => e.Description).ToList(),
                };
            }

            if (!await _roleManager.RoleExistsAsync(DefaultRole))
            {
                await _roleManager.CreateAsync(new IdentityRole(DefaultRole));
            }

            await _userManager.AddToRoleAsync(user, DefaultRole);

            _logger.LogInformation("Auto-provisioned local user from external IdP login: {Email}", user.Email);
        }

        return await BuildSuccessResponseAsync(user);
    }

    public async Task<MfaSetupDto> GetMfaSetupAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new MfaSetupDto();
        }

        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        return new MfaSetupDto
        {
            SharedKey = FormatKeyForDisplay(unformattedKey!),
            AuthenticatorUri = GenerateAuthenticatorUri(user.Email ?? user.UserName ?? user.Id, unformattedKey!),
        };
    }

    public async Task<AuthResponseDto> EnableMfaAsync(string userId, string code)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new AuthResponseDto { Succeeded = false, Errors = { "User not found." } };
        }

        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        if (!isValid)
        {
            return new AuthResponseDto { Succeeded = false, Errors = { "Invalid verification code." } };
        }

        user.MfaEnabled = true;
        await _userManager.SetTwoFactorEnabledAsync(user, true);
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("MFA enabled for user: {Email}", user.Email);

        return new AuthResponseDto { Succeeded = true, UserId = user.Id, Email = user.Email, MfaEnabled = true };
    }

    public async Task<AuthResponseDto> DisableMfaAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return new AuthResponseDto { Succeeded = false, Errors = { "User not found." } };
        }

        user.MfaEnabled = false;
        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);
        await _userManager.UpdateAsync(user);

        // Trusting a device only makes sense while MFA is on; drop any remembered devices.
        var trustedDevices = await _dbContext.MfaTrustedDevices.Where(d => d.UserId == user.Id).ToListAsync();
        if (trustedDevices.Count > 0)
        {
            _dbContext.MfaTrustedDevices.RemoveRange(trustedDevices);
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("MFA disabled for user: {Email}", user.Email);

        return new AuthResponseDto { Succeeded = true, UserId = user.Id, Email = user.Email, MfaEnabled = false };
    }

    public async Task<AuthResponseDto> VerifyMfaAsync(string userId, string code, bool rememberDevice = false)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || !user.MfaEnabled)
        {
            return new AuthResponseDto { Succeeded = false, Errors = { "Invalid request." } };
        }

        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        if (!isValid)
        {
            return new AuthResponseDto { Succeeded = false, Errors = { "Invalid verification code." } };
        }

        // Establishes the "Identity.Application" cookie alongside the JWT so the interactive
        // OIDC/IdentityServer flow (which challenges that cookie scheme) recognizes the user as
        // already signed in - without this, /connect/authorize keeps redirecting back to login
        // even after a successful Basic Authentication sign-in.
        await _signInManager.SignInAsync(user, isPersistent: true);
        await SignInToIdentityServerCookieSchemeAsync(user);

        var response = await BuildSuccessResponseAsync(user);

        if (rememberDevice)
        {
            var rawTrustedDeviceToken = _tokenService.GenerateRefreshToken();
            _dbContext.MfaTrustedDevices.Add(new MfaTrustedDevice
            {
                UserId = user.Id,
                TokenHash = _tokenService.HashRefreshToken(rawTrustedDeviceToken),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(MfaTrustedDeviceDays),
            });
            await _dbContext.SaveChangesAsync();

            response.TrustedDeviceToken = rawTrustedDeviceToken;
        }

        return response;
    }

    /// <summary>
    /// Signs the user into the "Cookies" authentication scheme - the scheme Program.cs configures
    /// as <c>AddAuthentication(...).DefaultScheme</c> and that Duende IdentityServer checks on
    /// every interactive <c>/connect/authorize</c> request to decide whether the browser already
    /// has an authenticated session. ASP.NET Core Identity's <see cref="SignInManager{TUser}.SignInAsync"/>
    /// only establishes the separate "Identity.Application" cookie scheme, which IdentityServer
    /// does NOT recognize here - without also signing into "Cookies", a Basic Authentication
    /// login/register/MFA-verify never satisfies IdentityServer's interactive login check, causing
    /// it to keep redirecting back to the login page in an infinite loop whenever a downstream
    /// OIDC Relying Party (e.g. the Test Application App / RESUME_AI / REPORT_GEN clients) starts
    /// an Authorization Code flow against this AuthBridge instance.
    /// </summary>
    private async Task SignInToIdentityServerCookieSchemeAsync(ApplicationUser user)
    {
        var principal = await _signInManager.CreateUserPrincipalAsync(user);
        await _signInManager.Context.SignInAsync("Cookies", principal, new AuthenticationProperties { IsPersistent = true });
    }

    private async Task<bool> IsTrustedDeviceAsync(string userId, string? rawTrustedDeviceToken)
    {
        if (string.IsNullOrWhiteSpace(rawTrustedDeviceToken))
        {
            return false;
        }

        var tokenHash = _tokenService.HashRefreshToken(rawTrustedDeviceToken);
        var device = await _dbContext.MfaTrustedDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.TokenHash == tokenHash);

        return device is not null && device.IsActive;
    }

    private static string FormatKeyForDisplay(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToUpperInvariant();
    }

    private static string GenerateAuthenticatorUri(string accountLabel, string unformattedKey)
    {
        const string format = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";
        return string.Format(
            format,
            UrlEncoder.Default.Encode(MfaIssuer),
            UrlEncoder.Default.Encode(accountLabel),
            unformattedKey);
    }

    private async Task<AuthResponseDto> BuildSuccessResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAtUtc) = _tokenService.GenerateAccessToken(user, roles);

        var rawRefreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays);

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokenService.HashRefreshToken(rawRefreshToken),
            ExpiresAtUtc = refreshTokenExpiresAtUtc,
        });
        await _dbContext.SaveChangesAsync();

        return new AuthResponseDto
        {
            Succeeded = true,
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            RefreshToken = rawRefreshToken,
            RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc,
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            MfaEnabled = user.MfaEnabled,
            Roles = roles.ToList(),
        };
    }
}
