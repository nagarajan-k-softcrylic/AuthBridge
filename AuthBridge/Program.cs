using System.Text;
using AuthBridge.Configuration;
using AuthBridge.Data;
using AuthBridge.Entities;
using AuthBridge.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SpaServices.AngularCli;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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

// Duende IdentityServer - issues OIDC/OAuth tokens consumed by the Angular UI and APIs.
// Bootstrap (in-memory) clients/scopes live in Configuration/IdentityServerConfig.cs; user
// accounts are backed by ASP.NET Core Identity (AspNetUsers, etc.) via AddAspNetIdentity.
builder.Services.AddIdentityServer()
    .AddInMemoryIdentityResources(IdentityServerConfig.IdentityResources)
    .AddInMemoryApiScopes(IdentityServerConfig.ApiScopes)
    .AddInMemoryClients(IdentityServerConfig.Clients)
    .AddAspNetIdentity<ApplicationUser>()
    .AddDeveloperSigningCredential(); // TODO: replace with a persisted signing credential before production

// Validates the JWTs issued by TokenService for the Basic Authentication (register/login) flow.
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddAuthentication(options =>
    {
        // AddIdentity() above already set DefaultChallengeScheme/DefaultAuthenticateScheme to
        // the cookie-based "Identity.Application" scheme. Without overriding them here too,
        // an unauthenticated [Authorize] request (e.g. GET /api/auth/me) gets challenged by the
        // cookie handler, which redirects (302 to the login page) instead of returning 401 -
        // breaking the Angular interceptor's silent-refresh-on-401 logic.
        options.DefaultScheme = "Bearer";
        options.DefaultChallengeScheme = "Bearer";
        options.DefaultAuthenticateScheme = "Bearer";
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
    });
    // TODO: chain .AddMicrosoftIdentityWebApp()/.AddSaml2() handlers here for Entra ID SSO and SAML.

builder.Services.AddAuthorization();

// Angular build output lands in wwwroot/browser.
builder.Services.AddSpaStaticFiles(configuration =>
{
    configuration.RootPath = "wwwroot/browser";
});

var app = builder.Build();

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
