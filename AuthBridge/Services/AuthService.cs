using AuthBridge.DTOs;
using AuthBridge.Entities;
using Microsoft.AspNetCore.Identity;

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
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _tokenService = tokenService;
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

    private async Task<AuthResponseDto> BuildSuccessResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAtUtc) = _tokenService.GenerateAccessToken(user, roles);

        return new AuthResponseDto
        {
            Succeeded = true,
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
        };
    }
}
