using System.IdentityModel.Tokens.Jwt;
using System.Text;
using AuthBridge.Configuration;
using AuthBridge.Data;
using AuthBridge.Entities;
using AuthBridge.Repositories;
using AuthBridge.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SpaServices.AngularCli;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// OIDC claim types should keep their original short names (e.g. "sub", "email") instead of
// being remapped to long legacy Microsoft URIs (e.g. ".../claims/identity/claims/nameidentifier").
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core - backs the authentication/registration (ASP.NET Core Identity) tables in SQL Server.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ASP.NET Core Identity - provides user registration, password hashing, roles, lockout, etc.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Register/Login (Basic Authentication) application services.
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Application Catalog (Application Access Management Portal) services.
builder.Services.AddScoped<IApplicationRepository, ApplicationRepository>();
builder.Services.AddScoped<IUserApplicationRepository, UserApplicationRepository>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IApplicationAccessService, ApplicationAccessService>();

// Read once up-front so both the "Identity.Application" and "Cookies" schemes below can be
// aligned with the JWT refresh token's lifetime (RefreshTokenDays).
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

// "Identity.Application" is set by SignInManager.SignInAsync on Basic Auth login/register and
// checked by IdentityServer's /connect/authorize. Matching its lifetime to the JWT refresh token
// avoids the OIDC session outliving (or expiring before) the rest of the user's session.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(jwtSettings.RefreshTokenDays);
    options.SlidingExpiration = true;
});

// Duende IdentityServer - issues OIDC/OAuth tokens consumed by the Angular UI and APIs.
// Bootstrap (in-memory) clients/scopes live in Configuration/IdentityServerConfig.cs; user
// accounts are backed by ASP.NET Core Identity (AspNetUsers, etc.) via AddAspNetIdentity.
builder.Services.AddIdentityServer()
    .AddInMemoryIdentityResources(IdentityServerConfig.IdentityResources)
    .AddInMemoryApiScopes(IdentityServerConfig.ApiScopes)
    // Clients are NOT a fixed in-memory list: ApplicationCatalogClientStore resolves the two
    // fixed infrastructure clients (Angular SPA, AuthBridge confidential OIDC client) plus any
    // downstream Application Catalog entry (RESUME_AI, REPORT_GEN, TEST_APP, ...) live from the
    // Applications table, using ApplicationCode as the ClientId.
    .AddClientStore<ApplicationCatalogClientStore>()
    .AddAspNetIdentity<ApplicationUser>()
    // Overrides the default ASP.NET Identity profile service so downstream Application Catalog
    // clients (RESUME_AI, REPORT_GEN, TEST_APP) requesting the "application_access" scope receive
    // an "app_access" claim per assigned ApplicationCode - see ApplicationProfileService.
    .AddProfileService<ApplicationProfileService>()
    .AddDeveloperSigningCredential(); // TODO: replace with a persisted signing credential before production

// Validates the JWTs issued by TokenService for the Basic Authentication (register/login) flow.
var oidcSection = builder.Configuration.GetSection("Oidc");
builder.Services.AddAuthentication(options =>
    {
        // Requirement: "Cookies" is the default scheme (holds the signed-in user's session
        // after the OIDC handshake completes); "oidc" is the default CHALLENGE scheme (so a
        // plain [Authorize] with no explicit scheme redirects to the Identity Provider's login
        // page). The JWT Bearer API flow (e.g. GET /api/auth/me) must keep returning 401 instead
        // of a redirect, so it opts out of these defaults via
        // [Authorize(AuthenticationSchemes = "Bearer")] on that endpoint - see AuthController.
        options.DefaultScheme = "Cookies";
        options.DefaultChallengeScheme = "oidc";
    })
    .AddCookie("Cookies", options =>
    {
        // Align the OIDC session cookie's lifetime with the JWT refresh token's lifetime
        // (RefreshTokenDays) so neither session outlives the other in a confusing way - e.g. the
        // "Cookies" session staying valid long after the JWT refresh token has expired (or vice
        // versa), which would make SSO re-login behave inconsistently with Basic Auth re-login.
        options.ExpireTimeSpan = TimeSpan.FromDays(jwtSettings.RefreshTokenDays);
        options.SlidingExpiration = true;
    })
    .AddOpenIdConnect("oidc", options =>
    {
        options.Authority = oidcSection["Authority"];
        options.RequireHttpsMetadata = oidcSection.GetValue<bool>("RequireHttpsMetadata");
        options.ClientId = oidcSection["ClientId"];
        options.ClientSecret = oidcSection["ClientSecret"];
        options.ResponseType = "code";
        options.SaveTokens = true;

        options.Scope.Clear();
        options.Scope.Add("openid");
        var apiScope = oidcSection["ApiScope"];
        if (!string.IsNullOrWhiteSpace(apiScope))
        {
            options.Scope.Add(apiScope);
        }
    })
    .AddJwtBearer("Bearer", options =>
    {
        options.RequireHttpsMetadata = false; // TODO: enforce HTTPS metadata in production
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
        // The Angular SPA no longer holds the JWT in JS-accessible storage; it's issued as an
        // httpOnly cookie (see AuthController.SetAuthCookie). Read it from there instead of
        // requiring an Authorization header, since httpOnly cookies can't be attached manually.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue("authbridge_token", out var cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            }
        };
    })
    // Microsoft Entra ID - external IdP for the standalone "Continue with SSO" option, separate
    // from the local "oidc" client above (different Authority, different callback path so the
    // two don't collide). Registered unconditionally (reading whatever is in config) so the
    // scheme always exists; AuthController.SsoLogin() guards actual use behind EntraId:Enabled.
    .AddOpenIdConnect("EntraId", options =>
    {
        var entraSection = builder.Configuration.GetSection("EntraId");
        var instance = entraSection["Instance"] ?? "https://login.microsoftonline.com/";
        var tenantId = entraSection["TenantId"];
        options.Authority = $"{instance.TrimEnd('/')}/{tenantId}/v2.0";
        options.ClientId = entraSection["ClientId"];
        options.ClientSecret = entraSection["ClientSecret"];
        options.ResponseType = "code";
        options.CallbackPath = "/signin-oidc-entra";
        options.SaveTokens = true;
        // Explicitly pin the sign-in scheme to "Cookies" instead of relying on the ambiguous
        // DefaultScheme fallback - ASP.NET Core Identity also registers its own cookie scheme
        // ("Identity.Application"), and without this, EntraCallback()'s [Authorize] could fail to
        // see the signed-in principal and fall through to a challenge on the local "oidc" client.
        options.SignInScheme = "Cookies";

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("email");
        options.Scope.Add("profile");

        // Forwards the "login_hint" set in AuthController.SsoLogin() (from the email the user
        // typed on the SSO tab) so Microsoft's login page targets that account directly instead
        // of showing a generic "pick an account"/"enter any email" screen.
        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                if (context.Properties.Items.TryGetValue("login_hint", out var loginHint) && !string.IsNullOrWhiteSpace(loginHint))
                {
                    context.ProtocolMessage.LoginHint = loginHint;
                }

                return Task.CompletedTask;
            },
            // Temporary diagnostics: surfaces the real reason the Entra ID handshake doesn't
            // result in an authenticated "Cookies" principal (e.g. token validation failure)
            // instead of silently falling through to the default challenge scheme.
            OnAuthenticationFailed = context =>
            {
                var diagLogger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("EntraId");
                diagLogger.LogError(context.Exception, "EntraId authentication failed.");
                return Task.CompletedTask;
            },
            OnRemoteFailure = context =>
            {
                var diagLogger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("EntraId");
                diagLogger.LogError(context.Failure, "EntraId remote failure.");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var diagLogger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("EntraId");
                diagLogger.LogInformation("EntraId token validated for {Name}.", context.Principal?.Identity?.Name);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Angular build output lands in wwwroot/browser.
builder.Services.AddSpaStaticFiles(configuration =>
{
    configuration.RootPath = "wwwroot/browser";
});

var app = builder.Build();

// One-time idempotent seed: registers the known future-application catalog entries and grants
// nagavjm@gmail.com access to all of them (ApplicationAdmin test account). Safe to run on every
// startup - skips any application whose ApplicationCode already exists.
using (var seedScope = app.Services.CreateScope())
{
    var seedDbContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var seedUserManager = seedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    var seedApplications = new[]
    {
        new Application
        {
            Name = "ResumeScreener AI",
            Description = "AI-powered resume screening and candidate shortlisting application.",
            ApplicationCode = "RESUME_AI",
            ApplicationUrl = "https://resumescreener.example.com",
            IsActive = true,
            CreatedBy = "system-seed",
        },
        new Application
        {
            Name = "Report Generator",
            Description = "Automated report generation application.",
            ApplicationCode = "REPORT_GEN",
            ApplicationUrl = "https://reportgenerator.example.com",
            IsActive = true,
            CreatedBy = "system-seed",
        },
        new Application
        {
            Name = "Test Application App",
            Description = "Test application used for QA/testing purposes.",
            ApplicationCode = "TEST_APP",
            ApplicationUrl = "https://localhost:7299",
            IsActive = true,
            CreatedBy = "system-seed",
        },
    };

    foreach (var seedApp in seedApplications)
    {
        var exists = await seedDbContext.Applications.AnyAsync(a => a.ApplicationCode == seedApp.ApplicationCode);
        if (!exists)
        {
            seedDbContext.Applications.Add(seedApp);
        }
    }

    await seedDbContext.SaveChangesAsync();

    var seedTargetUser = await seedUserManager.FindByEmailAsync("nagavjm@gmail.com");
    if (seedTargetUser is not null)
    {
        var catalogApps = await seedDbContext.Applications
            .Where(a => new[] { "RESUME_AI", "REPORT_GEN", "TEST_APP" }.Contains(a.ApplicationCode))
            .ToListAsync();

        foreach (var catalogApp in catalogApps)
        {
            var existingAssignment = await seedDbContext.UserApplications
                .FirstOrDefaultAsync(ua => ua.UserId == seedTargetUser.Id && ua.ApplicationId == catalogApp.Id);

            if (existingAssignment is null)
            {
                seedDbContext.UserApplications.Add(new UserApplication
                {
                    UserId = seedTargetUser.Id,
                    ApplicationId = catalogApp.Id,
                    IsActive = true,
                    AssignedBy = "system-seed",
                });
            }
            else if (!existingAssignment.IsActive)
            {
                existingAssignment.IsActive = true;
                existingAssignment.RevokedAtUtc = null;
                existingAssignment.RevokedBy = null;
            }
        }

        await seedDbContext.SaveChangesAsync();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseIdentityServer();
app.UseAuthentication();
app.UseAuthorization();

// Endpoint execution must be dispatched here (not auto-appended at the end of the
// pipeline) so that API controller routes are matched BEFORE the SPA fallback below.
// Without an explicit UseEndpoints(), ASP.NET Core defers endpoint dispatch to after
// all remaining middleware - including UseSpa() - causing every API request to be
// swallowed by the Angular dev-server proxy / SPA fallback (404 "Cannot POST ...").
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.UseStaticFiles();
app.UseSpaStaticFiles();

app.UseSpa(spa =>
{
    spa.Options.SourcePath = "ClientApp";

    if (app.Environment.IsDevelopment())
    {
        spa.UseProxyToSpaDevelopmentServer("http://localhost:4200");
    }
});

app.Run();
