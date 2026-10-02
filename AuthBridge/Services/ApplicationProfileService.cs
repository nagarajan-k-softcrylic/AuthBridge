using System.Security.Claims;
using AuthBridge.Entities;
using AuthBridge.Repositories;
using Duende.IdentityServer.AspNetIdentity;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Identity;

namespace AuthBridge.Services;

/// <summary>
/// Extends the default ASP.NET Identity profile service to support OIDC-based SSO for downstream
/// Application Catalog clients (ResumeScreener AI, Report Generator, Test Application App - see
/// <see cref="Configuration.IdentityServerConfig.Clients"/>). When a client requests the
/// "application_access" scope, the issued id_token/userinfo includes one "app_access" claim per
/// catalog <see cref="Entities.Application.ApplicationCode"/> the user currently has an active
/// <see cref="UserApplication"/> assignment to - letting the downstream app authorize the user
/// locally instead of calling back into AuthBridge.
/// </summary>
public class ApplicationProfileService : ProfileService<ApplicationUser>
{
    private readonly IUserApplicationRepository _userApplicationRepository;

    public ApplicationProfileService(
        UserManager<ApplicationUser> userManager,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IUserApplicationRepository userApplicationRepository)
        : base(userManager, claimsFactory)
    {
        _userApplicationRepository = userApplicationRepository;
    }

    protected override async Task GetProfileDataAsync(ProfileDataRequestContext context, ApplicationUser user)
    {
        var principal = await GetUserClaimsAsync(user);
        var claims = principal.Claims.ToList();

        if (context.RequestedResources.ParsedScopes.Any(s => s.ParsedName == "application_access"))
        {
            var assignments = await _userApplicationRepository.GetActiveForUserAsync(user.Id);
            foreach (var code in assignments
                         .Where(a => a.Application is { IsActive: true })
                         .Select(a => a.Application!.ApplicationCode)
                         .Distinct())
            {
                claims.Add(new Claim("app_access", code));
            }
        }

        context.AddRequestedClaims(claims);
    }
}
