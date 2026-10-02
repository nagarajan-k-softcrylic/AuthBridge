using AuthBridge.DTOs;
using AuthBridge.Entities;
using AuthBridge.Repositories;
using Microsoft.AspNetCore.Identity;

namespace AuthBridge.Services;

public class ApplicationService : IApplicationService
{
    private readonly IApplicationRepository _applicationRepository;
    private readonly IUserApplicationRepository _userApplicationRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IScimNotificationService _scimNotificationService;

    public ApplicationService(
        IApplicationRepository applicationRepository,
        IUserApplicationRepository userApplicationRepository,
        UserManager<ApplicationUser> userManager,
        IScimNotificationService scimNotificationService)
    {
        _applicationRepository = applicationRepository;
        _userApplicationRepository = userApplicationRepository;
        _userManager = userManager;
        _scimNotificationService = scimNotificationService;
    }

    public async Task<PagedResult<ApplicationDto>> SearchAsync(string? searchTerm, int page, int pageSize, string? currentUserId = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var (items, totalCount) = await _applicationRepository.SearchAsync(searchTerm, page, pageSize);

        var assignedIds = new HashSet<Guid>();
        if (!string.IsNullOrEmpty(currentUserId))
        {
            var assignments = await _userApplicationRepository.GetActiveForUserAsync(currentUserId);
            assignedIds = assignments.Select(a => a.ApplicationId).ToHashSet();
        }

        return new PagedResult<ApplicationDto>
        {
            Items = items.Select(a => ToDto(a, assignedIds.Contains(a.Id))).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<ApplicationDto?> GetByIdAsync(Guid id)
    {
        var app = await _applicationRepository.GetByIdAsync(id);
        return app is null ? null : ToDto(app, isAssigned: false);
    }

    public async Task<ApplicationDto> CreateAsync(CreateApplicationDto request, string createdBy)
    {
        if (await _applicationRepository.CodeExistsAsync(request.ApplicationCode))
        {
            throw new InvalidOperationException($"An application with code '{request.ApplicationCode}' already exists.");
        }

        var app = new Application
        {
            Name = request.Name,
            Description = request.Description,
            ApplicationCode = request.ApplicationCode,
            ApplicationUrl = request.ApplicationUrl,
            IconUrl = request.IconUrl,
            IsActive = true,
            CreatedBy = createdBy,
        };

        await _applicationRepository.AddAsync(app);
        await _applicationRepository.SaveChangesAsync();

        return ToDto(app, isAssigned: false);
    }

    public async Task<ApplicationDto?> UpdateAsync(Guid id, UpdateApplicationDto request, string updatedBy)
    {
        var app = await _applicationRepository.GetByIdAsync(id);
        if (app is null)
        {
            return null;
        }

        app.Name = request.Name;
        app.Description = request.Description;
        app.ApplicationUrl = request.ApplicationUrl;
        app.IconUrl = request.IconUrl;
        app.IsActive = request.IsActive;
        app.UpdatedAtUtc = DateTime.UtcNow;
        app.UpdatedBy = updatedBy;

        _applicationRepository.Update(app);
        await _applicationRepository.SaveChangesAsync();

        return ToDto(app, isAssigned: false);
    }

    public async Task<bool> DeleteAsync(Guid id, string deletedBy)
    {
        var app = await _applicationRepository.GetByIdAsync(id);
        if (app is null)
        {
            return false;
        }

        app.IsDeleted = true;
        app.DeletedAtUtc = DateTime.UtcNow;
        app.DeletedBy = deletedBy;

        _applicationRepository.Update(app);
        await _applicationRepository.SaveChangesAsync();
        return true;
    }

    public async Task<UserApplicationDto> AssignAsync(AssignApplicationDto request, string assignedBy)
    {
        var app = await _applicationRepository.GetByIdAsync(request.ApplicationId)
            ?? throw new InvalidOperationException("Application not found.");

        var user = await _userManager.FindByIdAsync(request.UserId)
            ?? throw new InvalidOperationException("User not found.");

        var roles = await _userManager.GetRolesAsync(user);

        var existing = await _userApplicationRepository.GetAsync(request.UserId, request.ApplicationId);
        if (existing is not null)
        {
            existing.IsActive = true;
            existing.AssignedAtUtc = DateTime.UtcNow;
            existing.AssignedBy = assignedBy;
            existing.RevokedAtUtc = null;
            existing.RevokedBy = null;
            _userApplicationRepository.Update(existing);
            await _userApplicationRepository.SaveChangesAsync();
            await NotifyAssignedAsync(app, user, roles);
            return ToUserApplicationDto(existing, app, user);
        }

        var userApp = new UserApplication
        {
            UserId = request.UserId,
            ApplicationId = request.ApplicationId,
            IsActive = true,
            AssignedBy = assignedBy,
        };

        await _userApplicationRepository.AddAsync(userApp);
        await _userApplicationRepository.SaveChangesAsync();
        await NotifyAssignedAsync(app, user, roles);

        return ToUserApplicationDto(userApp, app, user);
    }

    /// <summary>SCIM trigger: "Application Assigned" - creates the user (and roles/groups) in the
    /// target application. Only fired here (and in <see cref="AssignByEmailAsync"/> via this
    /// method) - never on user creation/login/logout/password/MFA/refresh.</summary>
    private Task NotifyAssignedAsync(Application app, ApplicationUser user, IList<string> roles) =>
        _scimNotificationService.NotifyApplicationAssignedAsync(
            app.ApplicationCode, user.Id, user.Email ?? user.UserName ?? user.Id, user.FirstName, user.LastName, roles.ToList());

    public async Task<UserApplicationDto> AssignByEmailAsync(AssignApplicationByEmailDto request, string assignedBy)
    {
        var user = await _userManager.FindByEmailAsync(request.Email)
            ?? throw new InvalidOperationException($"No user found with email '{request.Email}'.");

        return await AssignAsync(new AssignApplicationDto { UserId = user.Id, ApplicationId = request.ApplicationId }, assignedBy);
    }

    public async Task<bool> RevokeAsync(RevokeApplicationDto request, string revokedBy)
    {
        var existing = await _userApplicationRepository.GetAsync(request.UserId, request.ApplicationId);
        if (existing is null || !existing.IsActive)
        {
            return false;
        }

        existing.IsActive = false;
        existing.RevokedAtUtc = DateTime.UtcNow;
        existing.RevokedBy = revokedBy;

        _userApplicationRepository.Update(existing);
        await _userApplicationRepository.SaveChangesAsync();

        // SCIM trigger: "Application Revoked" - disables the user in this application only; the
        // user remains active in every other application they're still assigned to.
        var app = await _applicationRepository.GetByIdAsync(request.ApplicationId);
        if (app is not null)
        {
            await _scimNotificationService.NotifyApplicationRevokedAsync(app.ApplicationCode, request.UserId);
        }

        return true;
    }

    public async Task<List<UserApplicationDto>> GetMyApplicationsAsync(string userId)
    {
        var assignments = await _userApplicationRepository.GetActiveForUserAsync(userId);
        return assignments
            .Where(a => a.Application is not null && a.Application.IsActive)
            .Select(a => ToUserApplicationDto(a, a.Application!, user: null))
            .ToList();
    }

    public async Task<List<UserApplicationDto>> GetAssignmentsForApplicationAsync(Guid applicationId)
    {
        var assignments = await _userApplicationRepository.GetActiveForApplicationAsync(applicationId);
        var app = await _applicationRepository.GetByIdAsync(applicationId);
        return assignments
            .Select(a => ToUserApplicationDto(a, app, a.User))
            .ToList();
    }

    private static ApplicationDto ToDto(Application app, bool isAssigned) => new()
    {
        Id = app.Id,
        Name = app.Name,
        Description = app.Description,
        ApplicationCode = app.ApplicationCode,
        ApplicationUrl = app.ApplicationUrl,
        IconUrl = app.IconUrl,
        IsActive = app.IsActive,
        CreatedAtUtc = app.CreatedAtUtc,
        IsAssignedToCurrentUser = isAssigned,
    };

    private static UserApplicationDto ToUserApplicationDto(UserApplication ua, Application? app, ApplicationUser? user) => new()
    {
        Id = ua.Id,
        UserId = ua.UserId,
        UserEmail = user?.Email,
        ApplicationId = ua.ApplicationId,
        ApplicationName = app?.Name ?? string.Empty,
        ApplicationCode = app?.ApplicationCode ?? string.Empty,
        ApplicationUrl = app?.ApplicationUrl ?? string.Empty,
        IconUrl = app?.IconUrl,
        IsActive = ua.IsActive,
        AssignedAtUtc = ua.AssignedAtUtc,
        AssignedBy = ua.AssignedBy,
    };
}
