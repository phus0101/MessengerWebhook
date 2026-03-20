using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessengerWebhook.Migrations
{
    /// <inheritdoc />
    public partial class FixHighPriorityIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConversationSessions_FacebookPSID",
                table: "ConversationSessions");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_FacebookPSID",
                table: "ConversationSessions",
                column: "FacebookPSID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConversationSessions_FacebookPSID",
                table: "ConversationSessions");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_FacebookPSID",
                table: "ConversationSessions",
                column: "FacebookPSID");
        }
    }
}
