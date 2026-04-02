using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MessengerWebhook.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesBotFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Code",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductGiftMappings_ProductCode_GiftCode",
                table: "ProductGiftMappings");

            migrationBuilder.DropIndex(
                name: "IX_Gifts_Code",
                table: "Gifts");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ProductGiftMappings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Gifts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FacebookPageId",
                table: "ConversationSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ConversationSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ConversationMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BotConversationLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    HumanSupportCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    FacebookPSID = table.Column<string>(type: "text", nullable: false),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnlockAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotConversationLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HumanSupportCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerIdentityId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    FacebookPSID = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    TranscriptExcerpt = table.Column<string>(type: "text", nullable: false),
                    AssignedToEmail = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "text", nullable: true),
                    ResumeBotOnNextMessage = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HumanSupportCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: false),
                    SourceName = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerIdentities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FacebookPSID = table.Column<string>(type: "text", nullable: false),
                    FacebookPageId = table.Column<string>(type: "text", nullable: true),
                    FullName = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    ShippingAddress = table.Column<string>(type: "text", nullable: true),
                    TotalOrders = table.Column<int>(type: "integer", nullable: false),
                    SuccessfulDeliveries = table.Column<int>(type: "integer", nullable: false),
                    FailedDeliveries = table.Column<int>(type: "integer", nullable: false),
                    LifetimeValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LastInteractionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerIdentities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FacebookPageConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FacebookPageId = table.Column<string>(type: "text", nullable: false),
                    PageName = table.Column<string>(type: "text", nullable: false),
                    PageAccessToken = table.Column<string>(type: "text", nullable: true),
                    VerifyToken = table.Column<string>(type: "text", nullable: true),
                    AppSecretOverride = table.Column<string>(type: "text", nullable: true),
                    DefaultManagerEmail = table.Column<string>(type: "text", nullable: true),
                    IsPrimaryPage = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacebookPageConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacebookPageConfigs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DraftOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftCode = table.Column<string>(type: "text", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    CustomerIdentityId = table.Column<Guid>(type: "uuid", nullable: true),
                    FacebookPSID = table.Column<string>(type: "text", nullable: false),
                    CustomerName = table.Column<string>(type: "text", nullable: true),
                    CustomerPhone = table.Column<string>(type: "text", nullable: false),
                    ShippingAddress = table.Column<string>(type: "text", nullable: false),
                    MerchandiseTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ShippingFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrandTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    RequiresManualReview = table.Column<bool>(type: "boolean", nullable: false),
                    RiskSummary = table.Column<string>(type: "text", nullable: true),
                    CustomerNotes = table.Column<string>(type: "text", nullable: true),
                    AssignedManagerEmail = table.Column<string>(type: "text", nullable: true),
                    NobitaOrderId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DraftOrders_CustomerIdentities_CustomerIdentityId",
                        column: x => x.CustomerIdentityId,
                        principalTable: "CustomerIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DraftOrders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VipProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerIdentityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    IsVip = table.Column<bool>(type: "boolean", nullable: false),
                    TotalOrders = table.Column<int>(type: "integer", nullable: false),
                    LifetimeValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GreetingStyle = table.Column<string>(type: "text", nullable: false),
                    LastOrderAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VipProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VipProfiles_CustomerIdentities_CustomerIdentityId",
                        column: x => x.CustomerIdentityId,
                        principalTable: "CustomerIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ManagerProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FacebookPageConfigId = table.Column<Guid>(type: "uuid", nullable: true),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagerProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManagerProfiles_FacebookPageConfigs_FacebookPageConfigId",
                        column: x => x.FacebookPageConfigId,
                        principalTable: "FacebookPageConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ManagerProfiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DraftOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCode = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GiftCode = table.Column<string>(type: "text", nullable: true),
                    GiftName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DraftOrderItems_DraftOrders_DraftOrderId",
                        column: x => x.DraftOrderId,
                        principalTable: "DraftOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RiskSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerIdentityId = table.Column<Guid>(type: "uuid", nullable: true),
                    DraftOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    RequiresManualReview = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskSignals_CustomerIdentities_CustomerIdentityId",
                        column: x => x.CustomerIdentityId,
                        principalTable: "CustomerIdentities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RiskSignals_DraftOrders_DraftOrderId",
                        column: x => x.DraftOrderId,
                        principalTable: "DraftOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_Code",
                table: "Products",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductGiftMappings_TenantId_ProductCode_GiftCode",
                table: "ProductGiftMappings",
                columns: new[] { "TenantId", "ProductCode", "GiftCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_TenantId_Code",
                table: "Gifts",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationSessions_FacebookPageId",
                table: "ConversationSessions",
                column: "FacebookPageId");

            migrationBuilder.CreateIndex(
                name: "IX_BotConversationLocks_FacebookPSID_IsLocked",
                table: "BotConversationLocks",
                columns: new[] { "FacebookPSID", "IsLocked" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerIdentities_FacebookPSID",
                table: "CustomerIdentities",
                column: "FacebookPSID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerIdentities_PhoneNumber",
                table: "CustomerIdentities",
                column: "PhoneNumber");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerIdentities_TenantId",
                table: "CustomerIdentities",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftOrderItems_DraftOrderId",
                table: "DraftOrderItems",
                column: "DraftOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftOrders_CustomerIdentityId",
                table: "DraftOrders",
                column: "CustomerIdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftOrders_DraftCode",
                table: "DraftOrders",
                column: "DraftCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DraftOrders_Status",
                table: "DraftOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DraftOrders_TenantId",
                table: "DraftOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FacebookPageConfigs_FacebookPageId",
                table: "FacebookPageConfigs",
                column: "FacebookPageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacebookPageConfigs_TenantId",
                table: "FacebookPageConfigs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HumanSupportCases_FacebookPSID",
                table: "HumanSupportCases",
                column: "FacebookPSID");

            migrationBuilder.CreateIndex(
                name: "IX_HumanSupportCases_Status",
                table: "HumanSupportCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeSnapshots_Category_IsPublished",
                table: "KnowledgeSnapshots",
                columns: new[] { "Category", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_ManagerProfiles_Email",
                table: "ManagerProfiles",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerProfiles_FacebookPageConfigId",
                table: "ManagerProfiles",
                column: "FacebookPageConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_ManagerProfiles_TenantId",
                table: "ManagerProfiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskSignals_CustomerIdentityId",
                table: "RiskSignals",
                column: "CustomerIdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskSignals_DraftOrderId",
                table: "RiskSignals",
                column: "DraftOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskSignals_Level",
                table: "RiskSignals",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Code",
                table: "Tenants",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VipProfiles_CustomerIdentityId",
                table: "VipProfiles",
                column: "CustomerIdentityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BotConversationLocks");

            migrationBuilder.DropTable(
                name: "DraftOrderItems");

            migrationBuilder.DropTable(
                name: "HumanSupportCases");

            migrationBuilder.DropTable(
                name: "KnowledgeSnapshots");

            migrationBuilder.DropTable(
                name: "ManagerProfiles");

            migrationBuilder.DropTable(
                name: "RiskSignals");

            migrationBuilder.DropTable(
                name: "VipProfiles");

            migrationBuilder.DropTable(
                name: "FacebookPageConfigs");

            migrationBuilder.DropTable(
                name: "DraftOrders");

            migrationBuilder.DropTable(
                name: "CustomerIdentities");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_Code",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductGiftMappings_TenantId_ProductCode_GiftCode",
                table: "ProductGiftMappings");

            migrationBuilder.DropIndex(
                name: "IX_Gifts_TenantId_Code",
                table: "Gifts");

            migrationBuilder.DropIndex(
                name: "IX_ConversationSessions_FacebookPageId",
                table: "ConversationSessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProductGiftMappings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Gifts");

            migrationBuilder.DropColumn(
                name: "FacebookPageId",
                table: "ConversationSessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ConversationSessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ConversationMessages");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Code",
                table: "Products",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductGiftMappings_ProductCode_GiftCode",
                table: "ProductGiftMappings",
                columns: new[] { "ProductCode", "GiftCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gifts_Code",
                table: "Gifts",
                column: "Code",
                unique: true);
        }
    }
}
