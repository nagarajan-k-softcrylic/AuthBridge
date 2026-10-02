using Duende.IdentityServer.Models;

namespace AuthBridge.Configuration;

/// <summary>
/// Bootstrap (in-memory) Duende IdentityServer configuration.
/// Replace with persisted (EF Core) configuration stores once the data model is finalized.
/// </summary>
public static class IdentityServerConfig
{
    public static IEnumerable<IdentityResource> IdentityResources =>
        new List<IdentityResource>
        {
            new IdentityResources.OpenId(),
            new IdentityResources.Profile(),
        };

    public static IEnumerable<ApiScope> ApiScopes =>
        new List<ApiScope>
        {
            new ApiScope("authbridge.api", "AuthBridge API"),
            new ApiScope("authbridge", "AuthBridge API (OIDC confidential client)"),

            // Requested by downstream Application Catalog clients (ResumeScreener AI, Report
            // Generator, Test Application App) so their id_token/userinfo includes the "app_access"
            // claim (see ApplicationProfileService) listing which ApplicationCodes the signed-in
            // user is currently assigned to - letting each app authorize access without calling back.
            new ApiScope("application_access", "Application access claims (assigned ApplicationCodes)"),
        };

    public static IEnumerable<Client> Clients =>
        new List<Client>
        {
            // Angular SPA client using the OIDC Authorization Code flow with PKCE.
            new Client
            {
                ClientId = "authbridge.spa",
                ClientName = "AuthBridge Angular UI",
                AllowedGrantTypes = GrantTypes.Code,
                RequireClientSecret = false,
                RequirePkce = true,

                RedirectUris = { "http://localhost:4200/auth-callback" },
                PostLogoutRedirectUris = { "http://localhost:4200" },
                AllowedCorsOrigins = { "http://localhost:4200" },

                AllowedScopes =
                {
                    "openid",
                    "profile",
                    "authbridge.api",
                },

                AllowAccessTokensViaBrowser = true,
            },

            // Server-side (confidential) OIDC client used by Program.cs's
            // .AddOpenIdConnect("oidc", ...) handler - credentials come from the
            // "Oidc" section in appsettings.json and must match this entry exactly.
            new Client
            {
                ClientId = "1e52547a-efc0-4699-88bb-a62835b91233",
                ClientName = "AuthBridge OIDC Confidential Client",
                ClientSecrets = { new Secret("+t8g+yLv7Bs+XMjwos8QQWcM9aJbFTCN3FFSkiBXuuw=".Sha256()) },
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,

                RedirectUris = { "https://localhost:7199/signin-oidc" },
                PostLogoutRedirectUris = { "https://localhost:7199/signout-callback-oidc" },

                AllowedScopes =
                {
                    "openid",
                    "authbridge",
                },
            },
        };

    /// <summary>
    /// Builds an OIDC client definition for a downstream Application Catalog entry (<see
    /// cref="Entities.Application"/> row - RESUME_AI, REPORT_GEN, TEST_APP, etc). These are NOT
    /// hard-coded here: they are read from the database at request time by <see
    /// cref="Services.ApplicationCatalogClientStore"/>, which is the single source of truth (the
    /// same <c>Applications</c> table used by the catalog/search/launch endpoints). ApplicationCode
    /// is used as the ClientId; ApplicationUrl is used to derive the standard OIDC callback paths.
    /// Treated as a public client (PKCE, no secret) since catalog apps are browser-redirect based
    /// and AuthBridge does not currently persist a per-application client secret.
    /// </summary>
    public static Client BuildApplicationCatalogClient(Entities.Application application)
    {
        var baseUrl = application.ApplicationUrl.TrimEnd('/');
        return new Client
        {
            ClientId = application.ApplicationCode,
            ClientName = application.Name,
            AllowedGrantTypes = GrantTypes.Code,
            RequireClientSecret = false,
            RequirePkce = true,

            RedirectUris = { $"{baseUrl}/signin-oidc" },
            PostLogoutRedirectUris = { $"{baseUrl}/signout-callback-oidc" },
            AllowedCorsOrigins = { baseUrl },

            AllowedScopes = { "openid", "profile", "application_access" },
            AllowAccessTokensViaBrowser = true,

            // Ensures the "app_access" claim(s) added by ApplicationProfileService are embedded
            // directly in the id_token, not only retrievable via /connect/userinfo. Downstream
            // catalog apps (e.g. SmartHire) that only parse the id_token after the code/token
            // exchange (and don't separately call /connect/userinfo) still see app_access.
            AlwaysIncludeUserClaimsInIdToken = true,
        };
    }
}
