using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessengerWebhook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationMetrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    FacebookPSID = table.Column<string>(type: "text", nullable: false),
                    ABTestVariant = table.Column<string>(type: "text", nullable: false),
                    MessageTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConversationTurn = table.Column<int>(type: "integer", nullable: false),
                    TotalResponseTimeMs = table.Column<int>(type: "integer", nullable: false),
                    PipelineLatencyMs = table.Column<int>(type: "integer", nullable: true),
                    DetectedEmotion = table.Column<string>(type: "text", nullable: true),
                    EmotionConfidence = table.Column<decimal>(type: "numeric", nullable: true),
                    MatchedTone = table.Column<string>(type: "text", nullable: true),
                    JourneyStage = table.Column<string>(type: "text", nullable: true),
                    ValidationPassed = table.Column<bool>(type: "boolean", nullable: true),
                    ValidationErrors = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    ConversationOutcome = table.Column<string>(type: "text", nullable: true),
                    AdditionalMetrics = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMetrics_ConversationSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ConversationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationMetrics_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMetrics_ABTestVariant",
                table: "ConversationMetrics",
                column: "ABTestVariant");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMetrics_ConversationOutcome",
                table: "ConversationMetrics",
                column: "ConversationOutcome",
                filter: "\"ConversationOutcome\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMetrics_MessageTimestamp",
                table: "ConversationMetrics",
                column: "MessageTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMetrics_SessionId",
                table: "ConversationMetrics",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMetrics_TenantId",
                table: "ConversationMetrics",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationMetrics");
        }
    }
}
