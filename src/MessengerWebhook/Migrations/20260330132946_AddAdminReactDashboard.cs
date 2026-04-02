using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessengerWebhook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminReactDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NobitaLastSyncedAt",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NobitaProductId",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NobitaSyncError",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NobitaWeight",
                table: "Products",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "ManagerProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAt",
                table: "ManagerProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastLoginIp",
                table: "ManagerProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntil",
                table: "ManagerProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "ManagerProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimedByEmail",
                table: "HumanSupportCases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookPageId",
                table: "HumanSupportCases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastNotificationError",
                table: "HumanSupportCases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastNotificationSentAt",
                table: "HumanSupportCases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedByEmail",
                table: "HumanSupportCases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookPageId",
                table: "DraftOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSubmissionAttemptAt",
                table: "DraftOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSubmissionError",
                table: "DraftOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "DraftOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByEmail",
                table: "DraftOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubmissionAttemptCount",
                table: "DraftOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "DraftOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedByEmail",
                table: "DraftOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookPageId",
                table: "BotConversationLocks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FacebookPageId = table.Column<string>(type: "text", nullable: true),
                    ManagerProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorEmail = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    ResourceType = table.Column<string>(type: "text", nullable: false),
                    ResourceId = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HumanSupportCases_FacebookPageId",
                table: "HumanSupportCases",
                column: "FacebookPageId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftOrders_FacebookPageId",
                table: "DraftOrders",
                column: "FacebookPageId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_CreatedAt",
                table: "AdminAuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_ResourceType_ResourceId",
                table: "AdminAuditLogs",
                columns: new[] { "ResourceType", "ResourceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_HumanSupportCases_FacebookPageId",
                table: "HumanSupportCases");

            migrationBuilder.DropIndex(
                name: "IX_DraftOrders_FacebookPageId",
                table: "DraftOrders");

            migrationBuilder.DropColumn(
                name: "NobitaLastSyncedAt",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NobitaProductId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NobitaSyncError",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NobitaWeight",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                table: "ManagerProfiles");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "ManagerProfiles");

            migrationBuilder.DropColumn(
                name: "LastLoginIp",
                table: "ManagerProfiles");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                table: "ManagerProfiles");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "ManagerProfiles");

            migrationBuilder.DropColumn(
                name: "ClaimedByEmail",
                table: "HumanSupportCases");

            migrationBuilder.DropColumn(
                name: "FacebookPageId",
                table: "HumanSupportCases");

            migrationBuilder.DropColumn(
                name: "LastNotificationError",
                table: "HumanSupportCases");

            migrationBuilder.DropColumn(
                name: "LastNotificationSentAt",
                table: "HumanSupportCases");

            migrationBuilder.DropColumn(
                name: "ResolvedByEmail",
                table: "HumanSupportCases");

            migrationBuilder.DropColumn(
                name: "FacebookPageId",
                table: "DraftOrders");

            migrationBuilder.DropColumn(
                name: "LastSubmissionAttemptAt",
                table: "DraftOrders");

            migrationBuilder.DropColumn(
                name: "LastSubmissionError",
                table: "DraftOrders");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "DraftOrders");

            migrationBuilder.DropColumn(
                name: "ReviewedByEmail",
                table: "DraftOrders");

            migrationBuilder.DropColumn(
                name: "SubmissionAttemptCount",
                table: "DraftOrders");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "DraftOrders");

            migrationBuilder.DropColumn(
                name: "SubmittedByEmail",
                table: "DraftOrders");

            migrationBuilder.DropColumn(
                name: "FacebookPageId",
                table: "BotConversationLocks");
        }
    }
}
