using AuthBridge.Configuration;
using Microsoft.AspNetCore.SpaServices.AngularCli;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Duende IdentityServer - issues OIDC/OAuth tokens consumed by the Angular UI and APIs.
// Bootstrap (in-memory) configuration lives in Configuration/IdentityServerConfig.cs.
builder.Services.AddIdentityServer()
    .AddInMemoryIdentityResources(IdentityServerConfig.IdentityResources)
    .AddInMemoryApiScopes(IdentityServerConfig.ApiScopes)
    .AddInMemoryClients(IdentityServerConfig.Clients)
    .AddDeveloperSigningCredential(); // TODO: replace with a persisted signing credential before production

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "Bearer";
    })
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["IdentityServer:Authority"] ?? "https://localhost:5001";
        options.TokenValidationParameters.ValidateAudience = false;
        options.RequireHttpsMetadata = false; // TODO: enforce HTTPS metadata in production
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

app.UseIdentityServer();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.UseStaticFiles();
app.UseSpaStaticFiles();

app.UseSpa(spa =>
{
    spa.Options.SourcePath = "ClientApp";

    if (app.Environment.IsDevelopment())
    {
        spa.UseProxyToSpaDevelopmentServer("https://localhost:4200");
    }
});

app.Run();
