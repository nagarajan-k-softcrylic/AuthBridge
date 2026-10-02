using System.Security.Claims;
using AuthBridge.DTOs;
using AuthBridge.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthBridge.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string AuthCookieName = "authbridge_token";
    private const string RefreshCookieName = "authbridge_refresh_token";
    private const string TrustedDeviceCookieName = "authbridge_trusted_device";

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
        // SSO is available via the standalone OIDC flow (oidc-login/oidc-callback) as soon as
        // the Oidc client is configured - Entra ID remains a separate, not-yet-wired option.
        var ssoEnabled = !string.IsNullOrWhiteSpace(_configuration["Oidc:ClientId"])
            || (_configuration.GetValue<bool>("EntraId:Enabled") && !string.IsNullOrWhiteSpace(_configuration["EntraId:ClientId"]));

        return Ok(new AuthOptionsDto { SsoEnabled = ssoEnabled });
    }

    /// <summary>
    /// Starts the SSO (Entra ID) sign-in flow. Requires EntraId to be configured and enabled.
    /// The optional <paramref name="email"/> (collected on the login page's SSO tab) is passed
    /// through as an OIDC `login_hint` so Microsoft's login page pre-fills/targets that account
    /// instead of showing a generic "pick an account" screen.
    /// </summary>
    [HttpGet("sso-login")]
    public IActionResult SsoLogin([FromQuery] string? email)
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

        var properties = new AuthenticationProperties { RedirectUri = "/api/auth/entra-callback" };
        if (!string.IsNullOrWhiteSpace(email))
        {
            properties.Items["login_hint"] = email;
        }

        return Challenge(properties, "EntraId");
    }

    /// <summary>
    /// Reached after a successful Microsoft Entra ID sign-in. Auto-provisions (or matches by
    /// email) a local user, mints the same JWT/refresh-token cookie pair as Basic Authentication
    /// login does, then redirects the browser into the SPA.
    /// </summary>
    [Authorize(AuthenticationSchemes = "Cookies")]
    [HttpGet("entra-callback")]
    public async Task<IActionResult> EntraCallback()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("preferred_username");
        var firstName = User.FindFirstValue(ClaimTypes.GivenName);
        var lastName = User.FindFirstValue(ClaimTypes.Surname);

        var result = await _authService.IssueTokensForExternalUserAsync(email ?? string.Empty, firstName, lastName);
        if (!result.Succeeded)
        {
            return Unauthorized(result);
        }

        SetAuthCookies(result);
        return Redirect("/home");
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

        Request.Cookies.TryGetValue(TrustedDeviceCookieName, out var trustedDeviceToken);
        var result = await _authService.LoginAsync(request, trustedDeviceToken);

        if (!result.Succeeded)
        {
            return Unauthorized(result);
        }

        SetAuthCookies(result);
        return Ok(result);
    }

    /// <summary>
    /// Completes a login that returned <c>RequiresMfa = true</c>: validates the TOTP code from
    /// the user's authenticator app and, if valid, issues the normal JWT/refresh-token cookies.
    /// Anonymous because the user isn't fully signed in yet at this point in the flow.
    /// </summary>
    [HttpPost("mfa/verify")]
    public async Task<ActionResult<AuthResponseDto>> MfaVerify([FromBody] MfaVerifyRequestDto request)
    {
        var result = await _authService.VerifyMfaAsync(request.UserId, request.Code, request.RememberDevice);

        if (!result.Succeeded)
        {
            return Unauthorized(result);
        }

        SetAuthCookies(result);
        return Ok(result);
    }

    /// <summary>
    /// Generates (or reuses) the current user's TOTP shared key and otpauth:// URI so they can
    /// add the account to an authenticator app (Google Authenticator, Microsoft Authenticator,
    /// etc.) as the first step of enabling MFA.
    /// </summary>
    [Authorize(AuthenticationSchemes = "Bearer")]
    [HttpGet("mfa/setup")]
    public async Task<ActionResult<MfaSetupDto>> MfaSetup()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        return Ok(await _authService.GetMfaSetupAsync(userId));
    }

    /// <summary>
    /// Confirms MFA setup by validating a code generated from the authenticator app configured
    /// via <see cref="MfaSetup"/>. On success, MFA is turned on for the account.
    /// </summary>
    [Authorize(AuthenticationSchemes = "Bearer")]
    [HttpPost("mfa/enable")]
    public async Task<ActionResult<AuthResponseDto>> MfaEnable([FromBody] MfaEnableRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _authService.EnableMfaAsync(userId, request.Code);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>Turns off MFA for the current user.</summary>
    [Authorize(AuthenticationSchemes = "Bearer")]
    [HttpPost("mfa/disable")]
    public async Task<ActionResult<AuthResponseDto>> MfaDisable()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _authService.DisableMfaAsync(userId);
        if (!result.Succeeded)
        {
            return BadRequest(result);
        }

        DeleteTrustedDeviceCookie();
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

    /// <summary>
    /// Revokes the refresh token server-side and clears the JWT auth cookies, logging the user
    /// out of the Basic Authentication session. Also signs out of the "Cookies" and
    /// "Identity.Application" schemes so the OIDC/IdentityServer session set up by the SSO login
    /// option is cleared too - otherwise /connect/authorize would silently re-authenticate the
    /// user from the stale session cookie on their next "SSO Login" click, instead of prompting
    /// for login again.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var rawRefreshToken) && !string.IsNullOrEmpty(rawRefreshToken))
        {
            await _authService.RevokeRefreshTokenAsync(rawRefreshToken);
        }

        DeleteAuthCookies();
        await HttpContext.SignOutAsync("Cookies");
        await HttpContext.SignOutAsync("Identity.Application");
        return Ok();
    }

    /// <summary>Returns the currently authenticated user's profile, read from the auth cookie.</summary>
    /// <remarks>
    /// Explicitly pinned to the "Bearer" scheme: the app-wide default authentication scheme is
    /// now "Cookies" (default challenge "oidc") for the SSO flow. Without this override, an
    /// unauthenticated call here would be challenged by "oidc" and get a redirect instead of a
    /// 401 - breaking the Angular interceptor's silent-refresh-on-401 logic.
    /// </remarks>
    [Authorize(AuthenticationSchemes = "Bearer")]
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
            MfaEnabled = bool.TryParse(User.FindFirstValue("mfaEnabled"), out var mfaEnabled) && mfaEnabled,
        });
    }

    /// <summary>
    /// TEST-ONLY endpoint to manually verify the OIDC SSO flow. Uses no explicit
    /// AuthenticationSchemes, so it falls back to the app-wide defaults configured in
    /// Program.cs: DefaultScheme = "Cookies", DefaultChallengeScheme = "oidc". Hitting this
    /// route unauthenticated should redirect to the IdentityServer login page; after a
    /// successful login it should redirect back to /signin-oidc and then land here, returning
    /// the authenticated user's claims.
    /// </summary>
    [Authorize]
    [HttpGet("sso-test")]
    public IActionResult SsoTest()
    {
        return Ok(new
        {
            message = "SSO login succeeded.",
            claims = User.Claims.Select(c => new { c.Type, c.Value }),
        });
    }

    /// <summary>
    /// Starts the standalone OIDC SSO login option: redirects the browser into the "oidc"
    /// Authorization Code flow against IdentityServer. On completion the OIDC middleware calls
    /// back to <see cref="OidcCallback"/> (set as the post-login RedirectUri below), which bridges
    /// the resulting "Cookies"-authenticated identity into the SPA's usual JWT cookies.
    /// </summary>
    [HttpGet("oidc-login")]
    public IActionResult OidcLogin()
    {
        return Challenge(new AuthenticationProperties { RedirectUri = "/api/auth/oidc-callback" }, "oidc");
    }

    /// <summary>
    /// Reached after a successful "oidc-login" handshake, once the "Cookies" scheme has the
    /// authenticated principal. Mints the same JWT/refresh-token cookie pair as Basic
    /// Authentication login does, then redirects the browser into the SPA - so the standalone
    /// SSO option results in the same client-side session shape as password login.
    /// </summary>
    [Authorize]
    [HttpGet("oidc-callback")]
    public async Task<IActionResult> OidcCallback()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var result = await _authService.IssueTokensForOidcUserAsync(userId);
        if (!result.Succeeded)
        {
            return Unauthorized(result);
        }

        SetAuthCookies(result);
        return Redirect("/home");
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

        if (!string.IsNullOrEmpty(result.TrustedDeviceToken))
        {
            // Scoped to the login endpoint only - it's read solely by LoginAsync to decide
            // whether the MFA prompt can be skipped on this device.
            Response.Cookies.Append(TrustedDeviceCookieName, result.TrustedDeviceToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7),
                Path = "/api/auth",
            });
        }

        result.Token = null;
        result.RefreshToken = null;
        result.TrustedDeviceToken = null;
    }

    private void DeleteAuthCookies()
    {
        Response.Cookies.Delete(AuthCookieName, new CookieOptions { Path = "/" });
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = "/api/auth" });
    }

    // Intentionally not cleared by DeleteAuthCookies: the whole point of "remember this device"
    // is that it survives logout/refresh-failures so the next login on this device can still skip
    // the MFA prompt until the 7-day window naturally expires. Only an explicit MFA disable
    // revokes it (see AuthService.DisableMfaAsync).
    private void DeleteTrustedDeviceCookie()
    {
        Response.Cookies.Delete(TrustedDeviceCookieName, new CookieOptions { Path = "/api/auth" });
    }
}
