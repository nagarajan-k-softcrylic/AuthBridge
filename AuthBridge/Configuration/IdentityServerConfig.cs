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
}
