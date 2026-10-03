using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthBridge.Migrations
{
    /// <inheritdoc />
    public partial class SeedApplicationAdminRoleAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: only inserts if the ApplicationAdmin role exists, the target user
            // exists, and the assignment isn't already present (safe to re-run / no-op in
            // environments where nagavjm@gmail.com hasn't been created yet).
            migrationBuilder.Sql(@"
                INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
                SELECT u.[Id], r.[Id]
                FROM [AspNetUsers] u
                CROSS JOIN [AspNetRoles] r
                WHERE u.[Email] = 'nagavjm@gmail.com'
                  AND r.[Name] = 'ApplicationAdmin'
                  AND NOT EXISTS (
                      SELECT 1 FROM [AspNetUserRoles] ur
                      WHERE ur.[UserId] = u.[Id] AND ur.[RoleId] = r.[Id]
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE ur
                FROM [AspNetUserRoles] ur
                INNER JOIN [AspNetUsers] u ON u.[Id] = ur.[UserId]
                INNER JOIN [AspNetRoles] r ON r.[Id] = ur.[RoleId]
                WHERE u.[Email] = 'nagavjm@gmail.com'
                  AND r.[Name] = 'ApplicationAdmin';
            ");
        }
    }
}
