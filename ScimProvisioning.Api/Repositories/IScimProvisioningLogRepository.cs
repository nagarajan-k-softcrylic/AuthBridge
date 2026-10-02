using ScimProvisioning.Api.Entities;

namespace ScimProvisioning.Api.Repositories;

public interface IScimProvisioningLogRepository
{
    Task AddAsync(ScimProvisioningLog log);

    Task<List<ScimProvisioningLog>> GetFailedAsync(int maxResults = 100);

    Task<List<ScimProvisioningLog>> GetRecentAsync(int maxResults = 200);

    void Update(ScimProvisioningLog log);

    Task SaveChangesAsync();
}
