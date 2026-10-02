using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScimProvisioning.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScimApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ApplicationCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AccessToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScimApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScimApplicationAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ScimExternalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Provisioned = table.Column<bool>(type: "bit", nullable: false),
                    ProvisionedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScimApplicationAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScimApplicationAssignments_ScimApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "ScimApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScimProvisioningLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    OperationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponsePayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScimProvisioningLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScimProvisioningLogs_ScimApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "ScimApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScimApplicationAssignments_ApplicationId_UserId",
                table: "ScimApplicationAssignments",
                columns: new[] { "ApplicationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScimApplications_ApplicationCode",
                table: "ScimApplications",
                column: "ApplicationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScimProvisioningLogs_ApplicationId_UserId",
                table: "ScimProvisioningLogs",
                columns: new[] { "ApplicationId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ScimProvisioningLogs_Status",
                table: "ScimProvisioningLogs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScimApplicationAssignments");

            migrationBuilder.DropTable(
                name: "ScimProvisioningLogs");

            migrationBuilder.DropTable(
                name: "ScimApplications");
        }
    }
}
