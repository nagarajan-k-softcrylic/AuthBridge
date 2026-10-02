using ScimProvisioning.Api.Entities;

namespace ScimProvisioning.Api.Repositories;

public interface IScimApplicationRepository
{
    Task<ScimApplication?> GetByCodeAsync(string applicationCode);

    Task<ScimApplication?> GetByIdAsync(Guid id);

    Task<List<ScimApplication>> GetAllAsync();

    Task AddAsync(ScimApplication application);

    void Update(ScimApplication application);

    Task SaveChangesAsync();
}
