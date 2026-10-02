using AuthBridge.Entities;

namespace AuthBridge.Repositories;

/// <summary>Data access for catalog <see cref="Application"/> records.</summary>
public interface IApplicationRepository
{
    Task<Application?> GetByIdAsync(Guid id);

    Task<Application?> GetByCodeAsync(string applicationCode);

    /// <summary>Searches active (non-deleted) applications by name/description, paged.</summary>
    Task<(List<Application> Items, int TotalCount)> SearchAsync(string? searchTerm, int page, int pageSize);

    Task AddAsync(Application application);

    void Update(Application application);

    Task<bool> CodeExistsAsync(string applicationCode);

    Task SaveChangesAsync();
}
