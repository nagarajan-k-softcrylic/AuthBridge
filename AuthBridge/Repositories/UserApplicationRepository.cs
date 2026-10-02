using AuthBridge.Data;
using AuthBridge.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthBridge.Repositories;

public class UserApplicationRepository : IUserApplicationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public UserApplicationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserApplication?> GetAsync(string userId, Guid applicationId) =>
        _dbContext.UserApplications
            .Include(ua => ua.Application)
            .FirstOrDefaultAsync(ua => ua.UserId == userId && ua.ApplicationId == applicationId);

    public Task<List<UserApplication>> GetActiveForUserAsync(string userId) =>
        _dbContext.UserApplications
            .Include(ua => ua.Application)
            .Where(ua => ua.UserId == userId && ua.IsActive)
            .ToListAsync();

    public Task<List<UserApplication>> GetActiveForApplicationAsync(Guid applicationId) =>
        _dbContext.UserApplications
            .Include(ua => ua.User)
            .Where(ua => ua.ApplicationId == applicationId && ua.IsActive)
            .ToListAsync();

    public Task AddAsync(UserApplication userApplication)
    {
        _dbContext.UserApplications.Add(userApplication);
        return Task.CompletedTask;
    }

    public void Update(UserApplication userApplication)
    {
        _dbContext.UserApplications.Update(userApplication);
    }

    public Task SaveChangesAsync() => _dbContext.SaveChangesAsync();
}
