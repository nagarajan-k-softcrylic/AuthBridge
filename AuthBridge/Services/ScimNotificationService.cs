using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AuthBridge.Services;

public class ScimNotificationService : IScimNotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ScimNotificationService> _logger;

    public ScimNotificationService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<ScimNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public Task NotifyApplicationAssignedAsync(string applicationCode, string userId, string userName, string? firstName, string? lastName, IReadOnlyList<string> roles) =>
        PostAsync("api/provisioning/assign-user", new
        {
            applicationCode,
            userId,
            userName,
            firstName,
            lastName,
            roles,
            correlationId = Guid.NewGuid().ToString(),
        });

    public Task NotifyApplicationRevokedAsync(string applicationCode, string userId) =>
        PostAsync("api/provisioning/revoke-user", new
        {
            applicationCode,
            userId,
            correlationId = Guid.NewGuid().ToString(),
        });

    public Task NotifyUserProfileUpdatedAsync(string userId, string userName, string? firstName, string? lastName, bool active) =>
        PostAsync("api/provisioning/update-user", new
        {
            userId,
            userName,
            firstName,
            lastName,
            active,
            correlationId = Guid.NewGuid().ToString(),
        });

    public Task NotifyUserRoleUpdatedAsync(string applicationCode, string userId, IReadOnlyList<string> roles) =>
        PostAsync("api/provisioning/update-role", new
        {
            applicationCode,
            userId,
            roles,
            correlationId = Guid.NewGuid().ToString(),
        });

    private async Task PostAsync(string relativePath, object payload)
    {
        var baseUrl = _configuration["ScimProvisioning:BaseUrl"];
        var apiKey = _configuration["ScimProvisioning:ApiKey"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("SCIM provisioning notification skipped: ScimProvisioning:BaseUrl/ApiKey not configured.");
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("ScimProvisioning");
            client.BaseAddress = new Uri(baseUrl);
            var request = new HttpRequestMessage(HttpMethod.Post, relativePath) { Content = JsonContent.Create(payload) };
            request.Headers.Add("X-Api-Key", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("SCIM provisioning call to {Path} failed with {StatusCode}: {Body}", relativePath, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            // Never let a downstream SCIM failure break the AuthBridge operation that triggered
            // it; ScimProvisioning.Api's own audit log + RetryFailedProvisioningAsync recovers.
            _logger.LogError(ex, "SCIM provisioning call to {Path} threw an exception.", relativePath);
        }
    }
}
