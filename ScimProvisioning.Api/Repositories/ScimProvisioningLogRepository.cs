using Microsoft.EntityFrameworkCore;
using ScimProvisioning.Api.Data;
using ScimProvisioning.Api.Entities;

namespace ScimProvisioning.Api.Repositories;

public class ScimProvisioningLogRepository : IScimProvisioningLogRepository
{
    private readonly ScimDbContext _db;

    public ScimProvisioningLogRepository(ScimDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(ScimProvisioningLog log) => await _db.ScimProvisioningLogs.AddAsync(log);

    public Task<List<ScimProvisioningLog>> GetFailedAsync(int maxResults = 100) =>
        _db.ScimProvisioningLogs
            .Where(l => l.Status == ScimProvisioningStatus.Failed)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(maxResults)
            .ToListAsync();

    public Task<List<ScimProvisioningLog>> GetRecentAsync(int maxResults = 200) =>
        _db.ScimProvisioningLogs
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(maxResults)
            .ToListAsync();

    public void Update(ScimProvisioningLog log) => _db.ScimProvisioningLogs.Update(log);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
