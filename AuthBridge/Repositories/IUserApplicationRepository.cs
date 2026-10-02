using AuthBridge.Entities;

namespace AuthBridge.Repositories;

/// <summary>Data access for <see cref="UserApplication"/> assignment records.</summary>
public interface IUserApplicationRepository
{
    Task<UserApplication?> GetAsync(string userId, Guid applicationId);

    /// <summary>All active assignments for a user, including the related Application.</summary>
    Task<List<UserApplication>> GetActiveForUserAsync(string userId);

    /// <summary>All active assignments for an application, including the related User.</summary>
    Task<List<UserApplication>> GetActiveForApplicationAsync(Guid applicationId);

    Task AddAsync(UserApplication userApplication);

    void Update(UserApplication userApplication);

    Task SaveChangesAsync();
}
