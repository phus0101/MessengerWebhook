using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessengerWebhook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMetricsPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add composite index for metrics queries: (tenant_id, message_timestamp, ab_test_variant)
            migrationBuilder.CreateIndex(
                name: "IX_ConversationMetrics_TenantId_Timestamp_Variant",
                table: "ConversationMetrics",
                columns: new[] { "TenantId", "MessageTimestamp", "ABTestVariant" });

            // Add composite index for variant-first queries: (tenant_id, ab_test_variant, message_timestamp)
            migrationBuilder.CreateIndex(
                name: "IX_ConversationMetrics_TenantId_Variant_Timestamp",
                table: "ConversationMetrics",
                columns: new[] { "TenantId", "ABTestVariant", "MessageTimestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ConversationMetrics_TenantId_Timestamp_Variant",
                table: "ConversationMetrics");

            migrationBuilder.DropIndex(
                name: "IX_ConversationMetrics_TenantId_Variant_Timestamp",
                table: "ConversationMetrics");
        }
    }
}
