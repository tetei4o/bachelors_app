using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StoryCups.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoyaltyLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoyaltyActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScanSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ReversesActionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StampsAfter = table.Column<int>(type: "integer", nullable: false),
                    RewardsAfter = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyActions", x => x.Id);
                    table.CheckConstraint("CK_LoyaltyActions_BalanceAfter", "\"StampsAfter\" >= 0 AND \"RewardsAfter\" >= 0");
                    table.CheckConstraint("CK_LoyaltyActions_Reversal", "(\"Type\" = 'Reversal') = (\"ReversesActionId\" IS NOT NULL) AND (\"Type\" = 'Reversal') = (\"Reason\" IS NOT NULL)");
                    table.CheckConstraint("CK_LoyaltyActions_Sequence", "\"Sequence\" >= 1");
                    table.ForeignKey(
                        name: "FK_LoyaltyActions_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoyaltyActions_CustomerAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "CustomerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoyaltyActions_LoyaltyActions_ReversesActionId",
                        column: x => x.ReversesActionId,
                        principalTable: "LoyaltyActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoyaltyActions_ScanSessions_ScanSessionId",
                        column: x => x.ScanSessionId,
                        principalTable: "ScanSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    StampThreshold = table.Column<int>(type: "integer", nullable: false),
                    MaxStampsPerTransaction = table.Column<int>(type: "integer", nullable: false),
                    StampValidityDays = table.Column<int>(type: "integer", nullable: true),
                    UndoWindowMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltySettings", x => x.Id);
                    table.CheckConstraint("CK_LoyaltySettings_MaxStampsPerTransaction", "\"MaxStampsPerTransaction\" >= 1");
                    table.CheckConstraint("CK_LoyaltySettings_Singleton", "\"Id\" = 1");
                    table.CheckConstraint("CK_LoyaltySettings_StampThreshold", "\"StampThreshold\" >= 1");
                    table.CheckConstraint("CK_LoyaltySettings_StampValidityDays", "\"StampValidityDays\" IS NULL OR \"StampValidityDays\" >= 1");
                    table.CheckConstraint("CK_LoyaltySettings_UndoWindowMinutes", "\"UndoWindowMinutes\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VolumeMl = table.Column<int>(type: "integer", nullable: false),
                    PriceEur = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    EarnsStamp = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_PriceEur", "\"PriceEur\" >= 0");
                    table.CheckConstraint("CK_Products_VolumeMl", "\"VolumeMl\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Line = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StampDelta = table.Column<int>(type: "integer", nullable: false),
                    RewardDelta = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReversesEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_LedgerEntries_Line", "\"Line\" >= 1");
                    table.CheckConstraint("CK_LedgerEntries_NonZero", "\"StampDelta\" <> 0 OR \"RewardDelta\" <> 0");
                    table.ForeignKey(
                        name: "FK_LedgerEntries_CustomerAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "CustomerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerEntries_LedgerEntries_ReversesEntryId",
                        column: x => x.ReversesEntryId,
                        principalTable: "LedgerEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerEntries_LoyaltyActions_ActionId",
                        column: x => x.ActionId,
                        principalTable: "LoyaltyActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerEntries_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "LoyaltySettings",
                columns: new[] { "Id", "MaxStampsPerTransaction", "StampThreshold", "StampValidityDays", "UndoWindowMinutes" },
                values: new object[] { 1, 4, 8, null, 5 });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "EarnsStamp", "IsActive", "Name", "PriceEur", "SortOrder", "VolumeMl" },
                values: new object[,]
                {
                    { new Guid("0195f0a0-0000-7000-8000-000000000101"), true, true, "Single Espresso", 1.50m, 1, 30 },
                    { new Guid("0195f0a0-0000-7000-8000-000000000102"), true, true, "Double Espresso", 1.80m, 2, 60 },
                    { new Guid("0195f0a0-0000-7000-8000-000000000103"), true, true, "Cappuccino", 2.60m, 3, 240 },
                    { new Guid("0195f0a0-0000-7000-8000-000000000104"), true, true, "Flat White", 3.10m, 4, 240 },
                    { new Guid("0195f0a0-0000-7000-8000-000000000105"), true, true, "Cortado", 2.90m, 5, 130 },
                    { new Guid("0195f0a0-0000-7000-8000-000000000106"), true, true, "Latte", 3.10m, 6, 360 },
                    { new Guid("0195f0a0-0000-7000-8000-000000000107"), true, true, "Matcha Latte", 3.20m, 7, 240 },
                    { new Guid("0195f0a0-0000-7000-8000-000000000108"), true, true, "V60", 4.00m, 8, 250 },
                    { new Guid("0195f0a0-0000-7000-8000-000000000109"), true, true, "Cold Brew", 4.50m, 9, 200 },
                    { new Guid("0195f0a0-0000-7000-8000-00000000010a"), true, true, "Iced Latte", 3.40m, 10, 360 },
                    { new Guid("0195f0a0-0000-7000-8000-00000000010b"), true, true, "Iced Matcha Latte", 3.50m, 11, 360 },
                    { new Guid("0195f0a0-0000-7000-8000-00000000010c"), true, true, "Caffè Leccese", 2.30m, 12, 40 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_AccountId",
                table: "LedgerEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_ActionId_Line",
                table: "LedgerEntries",
                columns: new[] { "ActionId", "Line" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_ProductId",
                table: "LedgerEntries",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_ReversesEntryId",
                table: "LedgerEntries",
                column: "ReversesEntryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyActions_AccountId_Sequence",
                table: "LoyaltyActions",
                columns: new[] { "AccountId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyActions_ActorId_IdempotencyKey",
                table: "LoyaltyActions",
                columns: new[] { "ActorId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyActions_ReversesActionId",
                table: "LoyaltyActions",
                column: "ReversesActionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyActions_ScanSessionId",
                table: "LoyaltyActions",
                column: "ScanSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_SortOrder",
                table: "Products",
                column: "SortOrder");

            // Append-only (ADR-015): действията и записите в ledger-а само се добавят. Тригерите отхвърлят
            // UPDATE, DELETE и TRUNCATE, дори ако бъдещ код или ръчна заявка се опита да промени ред.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION storycups_reject_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Table "%" is append-only: % is not allowed.', TG_TABLE_NAME, TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER "TR_LedgerEntries_AppendOnly"
                    BEFORE UPDATE OR DELETE ON "LedgerEntries"
                    FOR EACH ROW EXECUTE FUNCTION storycups_reject_change();
                CREATE TRIGGER "TR_LedgerEntries_NoTruncate"
                    BEFORE TRUNCATE ON "LedgerEntries"
                    FOR EACH STATEMENT EXECUTE FUNCTION storycups_reject_change();

                CREATE TRIGGER "TR_LoyaltyActions_AppendOnly"
                    BEFORE UPDATE OR DELETE ON "LoyaltyActions"
                    FOR EACH ROW EXECUTE FUNCTION storycups_reject_change();
                CREATE TRIGGER "TR_LoyaltyActions_NoTruncate"
                    BEFORE TRUNCATE ON "LoyaltyActions"
                    FOR EACH STATEMENT EXECUTE FUNCTION storycups_reject_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "LoyaltySettings");

            migrationBuilder.DropTable(
                name: "LoyaltyActions");

            migrationBuilder.DropTable(
                name: "Products");

            // Тригерите изчезват заедно с таблиците; функцията се маха отделно.
            migrationBuilder.Sql("DROP FUNCTION storycups_reject_change();");
        }
    }
}
