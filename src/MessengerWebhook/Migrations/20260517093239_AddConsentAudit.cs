using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessengerWebhook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConsentAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentGivenAt",
                table: "CustomerIdentities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentPurposes",
                table: "CustomerIdentities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MarketingConsentGiven",
                table: "CustomerIdentities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ConsentAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerPsid = table.Column<string>(type: "text", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    ConsentTextShown = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WithdrawnReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentAuditRecords_TenantId_CustomerPsid_CreatedAt",
                table: "ConsentAuditRecords",
                columns: new[] { "TenantId", "CustomerPsid", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentAuditRecords_TenantId_CustomerPsid_Purpose",
                table: "ConsentAuditRecords",
                columns: new[] { "TenantId", "CustomerPsid", "Purpose" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentAuditRecords");

            migrationBuilder.DropColumn(
                name: "ConsentGivenAt",
                table: "CustomerIdentities");

            migrationBuilder.DropColumn(
                name: "ConsentPurposes",
                table: "CustomerIdentities");

            migrationBuilder.DropColumn(
                name: "MarketingConsentGiven",
                table: "CustomerIdentities");
        }
    }
}
