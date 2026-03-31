using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessengerWebhook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerMessageToRiskSignal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerMessage",
                table: "RiskSignals",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Backfill existing records with sanitized messages based on risk level
            migrationBuilder.Sql(@"
                UPDATE ""RiskSignals""
                SET ""CustomerMessage"" = CASE
                    WHEN ""Level"" = 2 THEN 'Đơn hàng cần xác nhận thêm thông tin'
                    WHEN ""Level"" = 1 THEN 'Đơn hàng đang được xử lý'
                    ELSE 'Đơn hàng hợp lệ'
                END
                WHERE ""CustomerMessage"" = '';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerMessage",
                table: "RiskSignals");
        }
    }
}
