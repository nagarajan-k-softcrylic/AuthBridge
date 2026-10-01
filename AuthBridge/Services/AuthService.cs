using AuthBridge.Configuration;
using AuthBridge.Data;
using AuthBridge.DTOs;
using AuthBridge.Entities;
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

        return await BuildSuccessResponseAsync(user);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
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

        if (user.MfaEnabled)
        {
            // TODO: integrate MFA verification step (e.g. TOTP/SMS) before issuing the access token.
            return new AuthResponseDto { Succeeded = true, RequiresMfa = true, UserId = user.Id, Email = user.Email };
        }

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
        };
    }
}
