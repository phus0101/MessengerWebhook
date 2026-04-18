using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessengerWebhook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddABTestVariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ABTestVariant",
                table: "ConversationSessions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_ABTestVariant",
                table: "ConversationSessions",
                column: "ABTestVariant");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConversationSessions_ABTestVariant",
                table: "ConversationSessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ABTestVariant",
                table: "ConversationSessions");
        }
    }
}
