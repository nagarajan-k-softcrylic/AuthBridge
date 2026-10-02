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

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<MfaTrustedDevice> MfaTrustedDevices => Set<MfaTrustedDevice>();

    public DbSet<Application> Applications => Set<Application>();

    public DbSet<UserApplication> UserApplications => Set<UserApplication>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.FirstName).HasMaxLength(100);
            entity.Property(u => u.LastName).HasMaxLength(100);
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MfaTrustedDevice>(entity =>
        {
            entity.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Application>(entity =>
        {
            entity.Property(a => a.Id).ValueGeneratedNever();
            entity.Property(a => a.Name).HasMaxLength(200).IsRequired();
            entity.Property(a => a.ApplicationCode).HasMaxLength(50).IsRequired();
            entity.Property(a => a.ApplicationUrl).HasMaxLength(500).IsRequired();
            entity.Property(a => a.Description).HasMaxLength(1000);
            entity.Property(a => a.IconUrl).HasMaxLength(500);
            entity.HasIndex(a => a.ApplicationCode).IsUnique();
            entity.HasQueryFilter(a => !a.IsDeleted);
        });

        builder.Entity<UserApplication>(entity =>
        {
            entity.HasOne(ua => ua.User)
                .WithMany()
                .HasForeignKey(ua => ua.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ua => ua.Application)
                .WithMany(a => a.UserApplications)
                .HasForeignKey(ua => ua.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(ua => new { ua.UserId, ua.ApplicationId }).IsUnique();

            // Mirrors Application's soft-delete query filter so EF doesn't warn about the
            // required relationship being filtered out on only one side.
            entity.HasQueryFilter(ua => ua.Application != null && !ua.Application.IsDeleted);
        });
    }
}
