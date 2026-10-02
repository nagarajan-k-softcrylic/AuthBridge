using AuthBridge.Repositories;

namespace AuthBridge.Services;

public class ApplicationAccessService : IApplicationAccessService
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IUserApplicationRepository _userApplicationRepository;

    public ApplicationAccessService(
        IApplicationRepository applicationRepository,
        IUserApplicationRepository userApplicationRepository)
    {
        _applicationRepository = applicationRepository;
        _userApplicationRepository = userApplicationRepository;
    }

    public async Task<bool> HasAccessAsync(string userId, Guid applicationId)
    {
        var app = await _applicationRepository.GetByIdAsync(applicationId);
        if (app is null || !app.IsActive)
        {
            return false;
        }

        var assignment = await _userApplicationRepository.GetAsync(userId, applicationId);
        return assignment is { IsActive: true };
    }

    public async Task<bool> HasAccessByCodeAsync(string userId, string applicationCode)
    {
        var app = await _applicationRepository.GetByCodeAsync(applicationCode);
        if (app is null)
        {
            return false;
        }

        return await HasAccessAsync(userId, app.Id);
    }
}
