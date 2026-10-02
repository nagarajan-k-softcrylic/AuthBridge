using Microsoft.EntityFrameworkCore;
using ScimProvisioning.Api.Data;
using ScimProvisioning.Api.Entities;

namespace ScimProvisioning.Api.Repositories;

public class ScimAssignmentRepository : IScimAssignmentRepository
{
    private readonly ScimDbContext _db;

    public ScimAssignmentRepository(ScimDbContext db)
    {
        _db = db;
    }

    public Task<ScimApplicationAssignment?> GetAsync(Guid applicationId, string userId) =>
        _db.ScimApplicationAssignments
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.UserId == userId);

    public Task<List<ScimApplicationAssignment>> GetActiveForUserAsync(string userId) =>
        _db.ScimApplicationAssignments
            .Include(a => a.Application)
            .Where(a => a.UserId == userId && a.IsActive)
            .ToListAsync();

    public async Task AddAsync(ScimApplicationAssignment assignment) =>
        await _db.ScimApplicationAssignments.AddAsync(assignment);

    public void Update(ScimApplicationAssignment assignment) => _db.ScimApplicationAssignments.Update(assignment);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
