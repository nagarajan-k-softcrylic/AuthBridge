using ScimProvisioning.Api.Entities;

namespace ScimProvisioning.Api.Repositories;

public interface IScimAssignmentRepository
{
    Task<ScimApplicationAssignment?> GetAsync(Guid applicationId, string userId);

    Task<List<ScimApplicationAssignment>> GetActiveForUserAsync(string userId);

    Task AddAsync(ScimApplicationAssignment assignment);

    void Update(ScimApplicationAssignment assignment);

    Task SaveChangesAsync();
}
