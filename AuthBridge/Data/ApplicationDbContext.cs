using AuthBridge.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthBridge.Data;

/// <summary>
/// EF Core database context. Inherits the standard ASP.NET Core Identity tables used for
/// authentication and registration (AspNetUsers, AspNetRoles, AspNetUserRoles,
/// AspNetUserClaims, AspNetRoleClaims, AspNetUserLogins, AspNetUserTokens).
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FirstName).HasMaxLength(100);
            entity.Property(u => u.LastName).HasMaxLength(100);
        });
    }
}
