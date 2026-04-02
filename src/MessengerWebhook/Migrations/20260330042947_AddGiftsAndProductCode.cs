using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessengerWebhook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGiftsAndProductCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Products_Code",
                table: "Products",
                column: "Code");

            migrationBuilder.CreateTable(
                name: "Gifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gifts", x => x.Id);
                    table.UniqueConstraint("AK_Gifts_Code", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "ProductGiftMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    GiftCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductGiftMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductGiftMappings_Gifts_GiftCode",
                        column: x => x.GiftCode,
                        principalTable: "Gifts",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductGiftMappings_Products_ProductCode",
                        column: x => x.ProductCode,
                        principalTable: "Products",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Code",
                table: "Products",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_Code",
                table: "Gifts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_IsActive",
                table: "Gifts",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ProductGiftMappings_GiftCode",
                table: "ProductGiftMappings",
                column: "GiftCode");

            migrationBuilder.CreateIndex(
                name: "IX_ProductGiftMappings_ProductCode",
                table: "ProductGiftMappings",
                column: "ProductCode");

            migrationBuilder.CreateIndex(
                name: "IX_ProductGiftMappings_ProductCode_GiftCode",
                table: "ProductGiftMappings",
                columns: new[] { "ProductCode", "GiftCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductGiftMappings");

            migrationBuilder.DropTable(
                name: "Gifts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Products_Code",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Code",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Products");

            migrationBuilder.AddColumn<float[]>(
                name: "Embedding",
                table: "Products",
                type: "real[]",
                nullable: true);
        }
    }
}
