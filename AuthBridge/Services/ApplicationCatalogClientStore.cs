using AuthBridge.Configuration;
using AuthBridge.Repositories;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;

namespace AuthBridge.Services;

/// <summary>
/// IdentityServer client store that resolves clients from two sources:
/// 1. The fixed infrastructure clients (Angular SPA, AuthBridge's own confidential OIDC client)
///    defined in <see cref="IdentityServerConfig.Clients"/>.
/// 2. Downstream Application Catalog entries (RESUME_AI, REPORT_GEN, TEST_APP, ...) looked up
///    live from the <c>Applications</c> table by <see cref="IApplicationRepository.GetByCodeAsync"/>,
///    using the application's <c>ApplicationCode</c> as the OIDC ClientId. This is what lets new
///    applications registered via the Application Catalog (create/search UI) become OIDC Relying
///    Parties automatically, without hard-coding each one's client registration in source code.
/// </summary>
public class ApplicationCatalogClientStore : IClientStore
{
    private readonly IApplicationRepository _applicationRepository;

    public ApplicationCatalogClientStore(IApplicationRepository applicationRepository)
    {
        _applicationRepository = applicationRepository;
    }

    public async Task<Client?> FindClientByIdAsync(string clientId)
    {
        var staticClient = IdentityServerConfig.Clients.FirstOrDefault(c => c.ClientId == clientId);
        if (staticClient is not null)
        {
            return staticClient;
        }

        var application = await _applicationRepository.GetByCodeAsync(clientId);
        if (application is null || !application.IsActive)
        {
            return null;
        }

        return IdentityServerConfig.BuildApplicationCatalogClient(application);
    }
}
