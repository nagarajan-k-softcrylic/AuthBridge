using AuthBridge.Data;
using AuthBridge.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthBridge.Repositories;

public class ApplicationRepository : IApplicationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ApplicationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Application?> GetByIdAsync(Guid id) =>
        _dbContext.Applications.FirstOrDefaultAsync(a => a.Id == id);

    public Task<Application?> GetByCodeAsync(string applicationCode) =>
        _dbContext.Applications.FirstOrDefaultAsync(a => a.ApplicationCode == applicationCode);

    public async Task<(List<Application> Items, int TotalCount)> SearchAsync(string? searchTerm, int page, int pageSize)
    {
        var query = _dbContext.Applications.AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(a =>
                EF.Functions.Like(a.Name, $"%{term}%") ||
                (a.Description != null && EF.Functions.Like(a.Description, $"%{term}%")) ||
                EF.Functions.Like(a.ApplicationCode, $"%{term}%"));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(a => a.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task AddAsync(Application application)
    {
        _dbContext.Applications.Add(application);
        return Task.CompletedTask;
    }

    public void Update(Application application)
    {
        _dbContext.Applications.Update(application);
    }

    public Task<bool> CodeExistsAsync(string applicationCode) =>
        _dbContext.Applications.AnyAsync(a => a.ApplicationCode == applicationCode);

    public Task SaveChangesAsync() => _dbContext.SaveChangesAsync();
}
