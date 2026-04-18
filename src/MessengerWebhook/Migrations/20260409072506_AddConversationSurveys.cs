using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessengerWebhook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationSurveys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SurveySent",
                table: "ConversationSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ConversationSurveys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    FacebookPsid = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ABTestVariant = table.Column<string>(type: "text", nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    FeedbackText = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationSurveys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationSurveys_ConversationSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ConversationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSurveys_ABTestVariant",
                table: "ConversationSurveys",
                column: "ABTestVariant");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSurveys_CreatedAt",
                table: "ConversationSurveys",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSurveys_Rating",
                table: "ConversationSurveys",
                column: "Rating");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSurveys_SessionId",
                table: "ConversationSurveys",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSurveys_TenantId",
                table: "ConversationSurveys",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationSurveys");

            migrationBuilder.DropColumn(
                name: "SurveySent",
                table: "ConversationSessions");
        }
    }
}
