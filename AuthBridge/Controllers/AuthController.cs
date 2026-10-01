using System.Security.Claims;
using AuthBridge.DTOs;
using AuthBridge.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthBridge.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string AuthCookieName = "authbridge_token";

    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public AuthController(IAuthService authService, IConfiguration configuration)
    {
        _authService = authService;
        _configuration = configuration;
    }

    /// <summary>
    /// Returns which authentication modes are available so the login page can render the
    /// Basic / SSO switch accordingly.
    /// </summary>
    [HttpGet("options")]
    public ActionResult<AuthOptionsDto> GetOptions()
    {
        var ssoEnabled = _configuration.GetValue<bool>("EntraId:Enabled")
            && !string.IsNullOrWhiteSpace(_configuration["EntraId:ClientId"]);

        return Ok(new AuthOptionsDto { SsoEnabled = ssoEnabled });
    }

    /// <summary>Starts the SSO (Entra ID) sign-in flow. Requires EntraId to be configured and enabled.</summary>
    [HttpGet("sso-login")]
    public IActionResult SsoLogin()
    {
        var ssoEnabled = _configuration.GetValue<bool>("EntraId:Enabled")
            && !string.IsNullOrWhiteSpace(_configuration["EntraId:ClientId"]);

        if (!ssoEnabled)
        {
            return StatusCode(StatusCodes.Status501NotImplemented, new AuthResponseDto
            {
                Succeeded = false,
                Errors = { "SSO is not configured. Set EntraId:Enabled and EntraId:ClientId in appsettings.json." },
            });
        }

        // TODO: once AddMicrosoftIdentityWebApp()/Entra handler is registered in Program.cs,
        // replace this with: return Challenge(new AuthenticationProperties { RedirectUri = "/" }, "EntraId");
        return StatusCode(StatusCodes.Status501NotImplemented, new AuthResponseDto
        {
            Succeeded = false,
            Errors = { "SSO handler is not yet wired up." },
        });
    }

    /// <summary>Registers a new user account (Basic Authentication flow).</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _authService.RegisterAsync(request);

        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        SetAuthCookie(result);
        return Ok(result);
    }

    /// <summary>Authenticates a user with email/password (Basic Authentication flow).</summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await _authService.LoginAsync(request);

        if (!result.Succeeded)
        {
            return Unauthorized(result);
        }

        SetAuthCookie(result);
        return Ok(result);
    }

    /// <summary>Clears the auth cookie, logging the current user out.</summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthCookieName, new CookieOptions { Path = "/" });
        return Ok();
    }

    /// <summary>Returns the currently authenticated user's profile, read from the auth cookie.</summary>
    [Authorize]
    [HttpGet("me")]
    public ActionResult<AuthResponseDto> Me()
    {
        return Ok(new AuthResponseDto
        {
            Succeeded = true,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
            Email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email"),
            FirstName = User.FindFirstValue("firstName"),
            LastName = User.FindFirstValue("lastName"),
        });
    }

    /// <summary>
    /// Stores the JWT in an httpOnly, Secure cookie so it's never exposed to JavaScript
    /// (mitigates XSS token theft). The response body never includes the raw token.
    /// </summary>
    private void SetAuthCookie(AuthResponseDto result)
    {
        if (string.IsNullOrEmpty(result.Token))
        {
            return;
        }

        Response.Cookies.Append(AuthCookieName, result.Token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = result.ExpiresAtUtc,
            Path = "/",
        });

        result.Token = null;
    }
}
