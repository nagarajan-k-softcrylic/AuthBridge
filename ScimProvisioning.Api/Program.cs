using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ScimProvisioning.Api.Data;
using ScimProvisioning.Api.Repositories;
using ScimProvisioning.Api.Security;
using ScimProvisioning.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Lets Swagger UI show an "Authorize" button for both supported schemes, so endpoints
    // secured with [Authorize(AuthenticationSchemes = "Bearer,ApiKey")] can be exercised
    // directly from the browser instead of needing an external HTTP client.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "AuthBridge-issued JWT access token.",
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKey",
        In = ParameterLocation.Header,
        Description = "Shared service-to-service API key (Security:ApiKey).",
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" } },
            Array.Empty<string>()
        },
    });
});

// EF Core - owns the ScimApplications / ScimProvisioningLogs / ScimApplicationAssignments tables.
// Deliberately a separate database from AuthBridge's: this service does not share AuthBridge's
// Identity tables and is not the source of truth for users.
builder.Services.AddDbContext<ScimDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IScimApplicationRepository, ScimApplicationRepository>();
builder.Services.AddScoped<IScimProvisioningLogRepository, ScimProvisioningLogRepository>();
builder.Services.AddScoped<IScimAssignmentRepository, ScimAssignmentRepository>();
builder.Services.AddScoped<IScimProvisioningService, ScimProvisioningService>();

// Outbound HTTP client used to call each downstream application's SCIM 2.0 endpoint.
builder.Services.AddHttpClient("ScimDownstream", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/scim+json"));
});

// Bearer JWT - validates tokens issued by AuthBridge (same Jwt:Key/Issuer/Audience), so an
// AuthBridge end-user with the "ScimAdmin" role can call the Admin UI/API endpoints directly.
// ApiKey - validates the shared service-to-service secret AuthBridge's backend uses when it
// calls /api/provisioning/* on behalf of its own request flow (no interactive user involved).
var jwtKey = builder.Configuration["Jwt:Key"] ?? string.Empty;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // TODO: enforce HTTPS metadata in production
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    })
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });

builder.Services.AddAuthorization(options =>
{
    // Only ScimAdmin users may manage SCIM applications, view logs/dashboard, or retry provisioning.
    options.AddPolicy("ScimAdmin", policy => policy.RequireRole("ScimAdmin"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program
{
}
