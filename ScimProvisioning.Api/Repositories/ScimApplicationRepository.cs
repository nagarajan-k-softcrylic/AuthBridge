using Microsoft.EntityFrameworkCore;
using ScimProvisioning.Api.Data;
using ScimProvisioning.Api.Entities;

namespace ScimProvisioning.Api.Repositories;

public class ScimApplicationRepository : IScimApplicationRepository
{
    private readonly ScimDbContext _db;

    public ScimApplicationRepository(ScimDbContext db)
    {
        _db = db;
    }

    public Task<ScimApplication?> GetByCodeAsync(string applicationCode) =>
        _db.ScimApplications.FirstOrDefaultAsync(a => a.ApplicationCode == applicationCode && a.IsActive);

    public Task<ScimApplication?> GetByIdAsync(Guid id) =>
        _db.ScimApplications.FirstOrDefaultAsync(a => a.Id == id);

    public Task<List<ScimApplication>> GetAllAsync() =>
        _db.ScimApplications.OrderBy(a => a.ApplicationName).ToListAsync();

    public async Task AddAsync(ScimApplication application) => await _db.ScimApplications.AddAsync(application);

    public void Update(ScimApplication application) => _db.ScimApplications.Update(application);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
