using Microsoft.EntityFrameworkCore;
using ScimProvisioning.Api.Entities;

namespace ScimProvisioning.Api.Data;

public class ScimDbContext : DbContext
{
    public ScimDbContext(DbContextOptions<ScimDbContext> options) : base(options)
    {
    }

    public DbSet<ScimApplication> ScimApplications => Set<ScimApplication>();

    public DbSet<ScimProvisioningLog> ScimProvisioningLogs => Set<ScimProvisioningLog>();

    public DbSet<ScimApplicationAssignment> ScimApplicationAssignments => Set<ScimApplicationAssignment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScimApplication>(builder =>
        {
            builder.HasIndex(a => a.ApplicationCode).IsUnique();
            builder.Property(a => a.ApplicationName).IsRequired().HasMaxLength(200);
            builder.Property(a => a.ApplicationCode).IsRequired().HasMaxLength(100);
            builder.Property(a => a.BaseUrl).IsRequired().HasMaxLength(500);
            builder.Property(a => a.AccessToken).IsRequired();
        });

        modelBuilder.Entity<ScimProvisioningLog>(builder =>
        {
            builder.Property(l => l.UserId).IsRequired().HasMaxLength(450);
            builder.Property(l => l.OperationType).HasConversion<string>().HasMaxLength(50);
            builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
            builder.HasOne(l => l.Application)
                .WithMany()
                .HasForeignKey(l => l.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(l => new { l.ApplicationId, l.UserId });
            builder.HasIndex(l => l.Status);
        });

        modelBuilder.Entity<ScimApplicationAssignment>(builder =>
        {
            builder.Property(a => a.UserId).IsRequired().HasMaxLength(450);
            builder.HasOne(a => a.Application)
                .WithMany(app => app.Assignments)
                .HasForeignKey(a => a.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(a => new { a.ApplicationId, a.UserId }).IsUnique();
        });
    }
}
