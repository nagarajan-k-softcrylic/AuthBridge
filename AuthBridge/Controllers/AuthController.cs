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
    private const string RefreshCookieName = "authbridge_refresh_token";

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

        SetAuthCookies(result);
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

        SetAuthCookies(result);
        return Ok(result);
    }

    /// <summary>
    /// Silently exchanges the long-lived refresh token cookie for a new short-lived access
    /// token (and a rotated refresh token), without requiring the user to log in again.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponseDto>> Refresh()
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var rawRefreshToken) || string.IsNullOrEmpty(rawRefreshToken))
        {
            return Unauthorized(new AuthResponseDto { Succeeded = false, Errors = { "No refresh token present." } });
        }

        var result = await _authService.RefreshAsync(rawRefreshToken);

        if (!result.Succeeded)
        {
            DeleteAuthCookies();
            return Unauthorized(result);
        }

        SetAuthCookies(result);
        return Ok(result);
    }

    /// <summary>Revokes the refresh token server-side and clears both auth cookies, logging the user out.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var rawRefreshToken) && !string.IsNullOrEmpty(rawRefreshToken))
        {
            await _authService.RevokeRefreshTokenAsync(rawRefreshToken);
        }

        DeleteAuthCookies();
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
    /// Stores the JWT access token and refresh token in separate httpOnly, Secure cookies so
    /// neither is ever exposed to JavaScript (mitigates XSS token theft). The response body
    /// never includes the raw token values.
    /// </summary>
    private void SetAuthCookies(AuthResponseDto result)
    {
        if (!string.IsNullOrEmpty(result.Token))
        {
            Response.Cookies.Append(AuthCookieName, result.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = result.ExpiresAtUtc,
                Path = "/",
            });
        }

        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            Response.Cookies.Append(RefreshCookieName, result.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = result.RefreshTokenExpiresAtUtc,
                // Scoped to the refresh/logout endpoints only - the raw refresh token never
                // needs to travel with ordinary API calls, limiting its exposure surface.
                Path = "/api/auth",
            });
        }

        result.Token = null;
        result.RefreshToken = null;
    }

    private void DeleteAuthCookies()
    {
        Response.Cookies.Delete(AuthCookieName, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = "/api/auth" });
    }
}
