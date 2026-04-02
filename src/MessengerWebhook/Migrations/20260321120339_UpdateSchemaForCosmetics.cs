using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessengerWebhook.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSchemaForCosmetics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_Colors_ColorId",
                table: "ProductVariants");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_Sizes_SizeId",
                table: "ProductVariants");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId_ColorId_SizeId",
                table: "ProductVariants");

            migrationBuilder.AlterColumn<string>(
                name: "SizeId",
                table: "ProductVariants",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ColorId",
                table: "ProductVariants",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Texture",
                table: "ProductVariants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "VolumeML",
                table: "ProductVariants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Convert Category from text to integer (enum)
            // Use USING clause to handle existing data
            migrationBuilder.Sql(@"
                ALTER TABLE ""Products""
                ALTER COLUMN ""Category"" TYPE integer
                USING 0;
            ");

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ContraindicationsJson",
                table: "Products",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IngredientsJson",
                table: "Products",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkinConcernsJson",
                table: "Products",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkinTypesJson",
                table: "Products",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Texture",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "pH",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConversationMessages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_ConversationSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ConversationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientCompatibilities",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Ingredient1 = table.Column<string>(type: "text", nullable: false),
                    Ingredient2 = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientCompatibilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SkinProfiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    SkinType = table.Column<string>(type: "text", nullable: false),
                    ConcernsJson = table.Column<string>(type: "jsonb", nullable: true),
                    SensitivitiesJson = table.Column<string>(type: "jsonb", nullable: true),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkinProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkinProfiles_ConversationSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ConversationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_VolumeML_Texture",
                table: "ProductVariants",
                columns: new[] { "ProductId", "VolumeML", "Texture" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_CreatedAt",
                table: "ConversationMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_SessionId",
                table: "ConversationMessages",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientCompatibilities_Ingredient1_Ingredient2",
                table: "IngredientCompatibilities",
                columns: new[] { "Ingredient1", "Ingredient2" });

            migrationBuilder.CreateIndex(
                name: "IX_SkinProfiles_SessionId",
                table: "SkinProfiles",
                column: "SessionId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Colors_ColorId",
                table: "ProductVariants",
                column: "ColorId",
                principalTable: "Colors",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Sizes_SizeId",
                table: "ProductVariants",
                column: "SizeId",
                principalTable: "Sizes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_Colors_ColorId",
                table: "ProductVariants");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariants_Sizes_SizeId",
                table: "ProductVariants");

            migrationBuilder.DropTable(
                name: "ConversationMessages");

            migrationBuilder.DropTable(
                name: "IngredientCompatibilities");

            migrationBuilder.DropTable(
                name: "SkinProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariants_ProductId_VolumeML_Texture",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "Texture",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "VolumeML",
                table: "ProductVariants");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ContraindicationsJson",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IngredientsJson",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SkinConcernsJson",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SkinTypesJson",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Texture",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "pH",
                table: "Products");

            migrationBuilder.AlterColumn<string>(
                name: "SizeId",
                table: "ProductVariants",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ColorId",
                table: "ProductVariants",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Products",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariants_ProductId_ColorId_SizeId",
                table: "ProductVariants",
                columns: new[] { "ProductId", "ColorId", "SizeId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Colors_ColorId",
                table: "ProductVariants",
                column: "ColorId",
                principalTable: "Colors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariants_Sizes_SizeId",
                table: "ProductVariants",
                column: "SizeId",
                principalTable: "Sizes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
