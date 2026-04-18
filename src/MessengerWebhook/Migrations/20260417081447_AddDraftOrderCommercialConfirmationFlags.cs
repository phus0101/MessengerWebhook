using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessengerWebhook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDraftOrderCommercialConfirmationFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InventoryConfirmed",
                table: "DraftOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PriceConfirmed",
                table: "DraftOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PromotionConfirmed",
                table: "DraftOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShippingConfirmed",
                table: "DraftOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
                UPDATE ""DraftOrders""
                SET ""PriceConfirmed"" = TRUE,
                    ""ShippingConfirmed"" = TRUE
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InventoryConfirmed",
                table: "DraftOrders");

            migrationBuilder.DropColumn(
                name: "PriceConfirmed",
                table: "DraftOrders");

            migrationBuilder.DropColumn(
                name: "PromotionConfirmed",
                table: "DraftOrders");

            migrationBuilder.DropColumn(
                name: "ShippingConfirmed",
                table: "DraftOrders");
        }
    }
}
