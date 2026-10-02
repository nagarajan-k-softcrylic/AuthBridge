using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ScimProvisioning.Api.DTOs;
using ScimProvisioning.Api.Entities;
using ScimProvisioning.Api.Repositories;
using ScimProvisioning.Api.Scim;

namespace ScimProvisioning.Api.Services;

public class ScimProvisioningService : IScimProvisioningService
{
    private readonly IScimApplicationRepository _applications;
    private readonly IScimAssignmentRepository _assignments;
    private readonly IScimProvisioningLogRepository _logs;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ScimProvisioningService> _logger;

    public ScimProvisioningService(
        IScimApplicationRepository applications,
        IScimAssignmentRepository assignments,
        IScimProvisioningLogRepository logs,
        IHttpClientFactory httpClientFactory,
        ILogger<ScimProvisioningService> logger)
    {
        _applications = applications;
        _assignments = assignments;
        _logs = logs;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ProvisioningResultDto> ProvisionApplicationAsync(AssignUserRequest request)
    {
        var result = await CreateUserAsync(request);
        if (result.Success && request.Roles.Count > 0)
        {
            await AssignGroupAsync(new UpdateUserRoleRequest
            {
                ApplicationCode = request.ApplicationCode,
                UserId = request.UserId,
                Roles = request.Roles,
                CorrelationId = request.CorrelationId,
            });
        }

        return result;
    }

    public async Task<ProvisioningResultDto> CreateUserAsync(AssignUserRequest request)
    {
        var app = await _applications.GetByCodeAsync(request.ApplicationCode);
        if (app is null)
        {
            return Fail($"No active SCIM application registered for code '{request.ApplicationCode}'.");
        }

        var scimUser = new ScimUser
        {
            UserName = request.UserName,
            Active = true,
            Name = new ScimName { GivenName = request.FirstName, FamilyName = request.LastName },
            Emails = new List<ScimEmail> { new() { Value = request.UserName, Primary = true } },
        };

        var (success, responseBody, externalId, error) = await SendAsync(app, HttpMethod.Post, "Users", scimUser);

        await LogAsync(app.Id, request.UserId, ScimOperationType.CreateUser, scimUser, responseBody, success, error, request.CorrelationId);

        var assignment = await _assignments.GetAsync(app.Id, request.UserId);
        if (assignment is null)
        {
            assignment = new ScimApplicationAssignment { ApplicationId = app.Id, UserId = request.UserId };
            await _assignments.AddAsync(assignment);
        }

        assignment.IsActive = true;
        assignment.Provisioned = success;
        assignment.ScimExternalId = externalId ?? assignment.ScimExternalId;
        assignment.ProvisionedAtUtc = success ? DateTime.UtcNow : assignment.ProvisionedAtUtc;
        assignment.LastSyncUtc = DateTime.UtcNow;
        await _assignments.SaveChangesAsync();

        return success
            ? new ProvisioningResultDto { Success = true, ScimExternalId = externalId }
            : Fail(error ?? "SCIM create user failed.");
    }

    public async Task<ProvisioningResultDto> UpdateUserAsync(UpdateUserProfileRequest request)
    {
        var activeAssignments = await _assignments.GetActiveForUserAsync(request.UserId);
        if (activeAssignments.Count == 0)
        {
            return new ProvisioningResultDto { Success = true, Message = "No assigned applications to update." };
        }

        var anyFailed = false;
        foreach (var assignment in activeAssignments)
        {
            var app = assignment.Application;
            if (app is null || string.IsNullOrEmpty(assignment.ScimExternalId))
            {
                continue;
            }

            var scimUser = new ScimUser
            {
                Id = assignment.ScimExternalId,
                UserName = request.UserName,
                Active = request.Active,
                Name = new ScimName { GivenName = request.FirstName, FamilyName = request.LastName },
                Emails = new List<ScimEmail> { new() { Value = request.UserName, Primary = true } },
            };

            var (success, responseBody, _, error) = await SendAsync(app, HttpMethod.Put, $"Users/{assignment.ScimExternalId}", scimUser);
            await LogAsync(app.Id, request.UserId, ScimOperationType.UpdateUser, scimUser, responseBody, success, error, request.CorrelationId);

            assignment.LastSyncUtc = DateTime.UtcNow;
            anyFailed |= !success;
        }

        await _assignments.SaveChangesAsync();
        return anyFailed
            ? Fail("One or more downstream applications failed to update.")
            : new ProvisioningResultDto { Success = true };
    }

    public async Task<ProvisioningResultDto> DisableUserAsync(RevokeUserRequest request)
    {
        var app = await _applications.GetByCodeAsync(request.ApplicationCode);
        var assignment = app is null ? null : await _assignments.GetAsync(app.Id, request.UserId);
        if (app is null || assignment is null || string.IsNullOrEmpty(assignment.ScimExternalId))
        {
            return Fail("No existing SCIM provisioning record found to disable.");
        }

        var patch = new ScimPatchRequest
        {
            Operations = new List<ScimPatchOperation> { new() { Op = "replace", Path = "active", Value = false } },
        };

        var (success, responseBody, _, error) = await SendAsync(app, HttpMethod.Patch, $"Users/{assignment.ScimExternalId}", patch);
        await LogAsync(app.Id, request.UserId, ScimOperationType.DisableUser, patch, responseBody, success, error, request.CorrelationId);

        assignment.IsActive = false;
        assignment.LastSyncUtc = DateTime.UtcNow;
        await _assignments.SaveChangesAsync();

        return success ? new ProvisioningResultDto { Success = true } : Fail(error ?? "SCIM disable user failed.");
    }

    public async Task<ProvisioningResultDto> DeleteUserAsync(RevokeUserRequest request)
    {
        var app = await _applications.GetByCodeAsync(request.ApplicationCode);
        var assignment = app is null ? null : await _assignments.GetAsync(app.Id, request.UserId);
        if (app is null || assignment is null || string.IsNullOrEmpty(assignment.ScimExternalId))
        {
            return Fail("No existing SCIM provisioning record found to delete.");
        }

        var (success, responseBody, _, error) = await SendAsync(app, HttpMethod.Delete, $"Users/{assignment.ScimExternalId}", null);
        await LogAsync(app.Id, request.UserId, ScimOperationType.DeleteUser, null, responseBody, success, error, request.CorrelationId);

        assignment.IsActive = false;
        assignment.Provisioned = false;
        assignment.LastSyncUtc = DateTime.UtcNow;
        await _assignments.SaveChangesAsync();

        return success ? new ProvisioningResultDto { Success = true } : Fail(error ?? "SCIM delete user failed.");
    }

    public async Task<ProvisioningResultDto> AssignGroupAsync(UpdateUserRoleRequest request) =>
        await PatchGroupsAsync(request, add: true);

    public async Task<ProvisioningResultDto> RemoveGroupAsync(UpdateUserRoleRequest request) =>
        await PatchGroupsAsync(request, add: false);

    private async Task<ProvisioningResultDto> PatchGroupsAsync(UpdateUserRoleRequest request, bool add)
    {
        var app = await _applications.GetByCodeAsync(request.ApplicationCode);
        var assignment = app is null ? null : await _assignments.GetAsync(app.Id, request.UserId);
        if (app is null || assignment is null || string.IsNullOrEmpty(assignment.ScimExternalId))
        {
            return Fail("No existing SCIM provisioning record found for role/group update.");
        }

        var patch = new ScimPatchRequest
        {
            Operations = request.Roles.Select(role => new ScimPatchOperation
            {
                Op = add ? "add" : "remove",
                Path = "groups",
                Value = new { display = role },
            }).ToList(),
        };

        var (success, responseBody, _, error) = await SendAsync(app, HttpMethod.Patch, $"Users/{assignment.ScimExternalId}", patch);
        var opType = add ? ScimOperationType.AssignGroup : ScimOperationType.RemoveGroup;
        await LogAsync(app.Id, request.UserId, opType, patch, responseBody, success, error, request.CorrelationId);

        assignment.LastSyncUtc = DateTime.UtcNow;
        await _assignments.SaveChangesAsync();

        return success ? new ProvisioningResultDto { Success = true } : Fail(error ?? "SCIM group update failed.");
    }

    public async Task<int> RetryFailedProvisioningAsync()
    {
        var failed = await _logs.GetFailedAsync();
        var retried = 0;
        foreach (var log in failed)
        {
            var app = await _applications.GetByIdAsync(log.ApplicationId);
            if (app is null)
            {
                continue;
            }

            var path = log.OperationType == ScimOperationType.CreateUser ? "Users" : $"Users/{log.UserId}";
            var method = log.OperationType switch
            {
                ScimOperationType.CreateUser => HttpMethod.Post,
                ScimOperationType.UpdateUser => HttpMethod.Put,
                ScimOperationType.DeleteUser => HttpMethod.Delete,
                _ => HttpMethod.Patch,
            };

            var (success, responseBody, _, error) = await SendRawAsync(app, method, path, log.RequestPayload);
            log.ResponsePayload = responseBody;
            log.Status = success ? ScimProvisioningStatus.Success : ScimProvisioningStatus.Failed;
            log.ErrorMessage = error;
            log.RetryCount += 1;
            _logs.Update(log);
            retried++;
        }

        await _logs.SaveChangesAsync();
        return retried;
    }

    private async Task<(bool Success, string? ResponseBody, string? ExternalId, string? Error)> SendAsync(
        Entities.ScimApplication app, HttpMethod method, string relativePath, object? payload)
    {
        var json = payload is null ? null : System.Text.Json.JsonSerializer.Serialize(payload);
        return await SendRawAsync(app, method, relativePath, json);
    }

    private async Task<(bool Success, string? ResponseBody, string? ExternalId, string? Error)> SendRawAsync(
        Entities.ScimApplication app, HttpMethod method, string relativePath, string? json)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ScimDownstream");
            var request = new HttpRequestMessage(method, $"{app.BaseUrl.TrimEnd('/')}/{relativePath}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", app.AccessToken);
            if (json is not null)
            {
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/scim+json");
            }

            var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return (false, body, null, $"Downstream returned {(int)response.StatusCode}.");
            }

            string? externalId = null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("id", out var idEl))
                {
                    externalId = idEl.GetString();
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Non-JSON or empty body (e.g. 204 on delete) - not an error.
            }

            return (true, body, externalId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SCIM call to {Application} failed.", app.ApplicationCode);
            return (false, null, null, ex.Message);
        }
    }

    private async Task LogAsync(
        Guid applicationId, string userId, ScimOperationType operation, object? request, string? response,
        bool success, string? error, string? correlationId)
    {
        await _logs.AddAsync(new ScimProvisioningLog
        {
            ApplicationId = applicationId,
            UserId = userId,
            OperationType = operation,
            RequestPayload = request is null ? null : System.Text.Json.JsonSerializer.Serialize(request),
            ResponsePayload = response,
            Status = success ? ScimProvisioningStatus.Success : ScimProvisioningStatus.Failed,
            ErrorMessage = error,
            CorrelationId = correlationId,
        });
        await _logs.SaveChangesAsync();
    }

    private static ProvisioningResultDto Fail(string message) => new() { Success = false, Message = message };
}
