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
        };
}
