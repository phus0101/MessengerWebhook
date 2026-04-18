using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessengerWebhook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftOrderSubmissionCoordinationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CustomerMetricsAppliedAt",
                table: "DraftOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmissionClaimedAt",
                table: "DraftOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionVersionToken",
                table: "DraftOrders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerMetricsAppliedAt",
                table: "DraftOrders");

            migrationBuilder.DropColumn(
                name: "SubmissionClaimedAt",
                table: "DraftOrders");

            migrationBuilder.DropColumn(
                name: "SubmissionVersionToken",
                table: "DraftOrders");
        }
    }
}
