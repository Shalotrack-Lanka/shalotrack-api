using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShaloTrack_API.Migrations
{
    /// <inheritdoc />
    public partial class AddRenewalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RenewalRequests",
                columns: table => new
                {
                    RenewalRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImeiNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AmountLkr = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CustomerNote = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SlipUploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SlipSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenewalRequests", x => x.RenewalRequestId);
                    table.ForeignKey(
                        name: "FK_RenewalRequests_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RenewalRequests_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RenewalSlips",
                columns: table => new
                {
                    RenewalRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    SizeBytes = table.Column<int>(type: "integer", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenewalSlips", x => x.RenewalRequestId);
                    table.ForeignKey(
                        name: "FK_RenewalSlips_RenewalRequests_RenewalRequestId",
                        column: x => x.RenewalRequestId,
                        principalTable: "RenewalRequests",
                        principalColumn: "RenewalRequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RenewalRequests_CustomerId_CreatedAt",
                table: "RenewalRequests",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RenewalRequests_OpenPerVehicle",
                table: "RenewalRequests",
                column: "VehicleId",
                unique: true,
                filter: "\"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "IX_RenewalRequests_Status_CreatedAt",
                table: "RenewalRequests",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RenewalSlips");

            migrationBuilder.DropTable(
                name: "RenewalRequests");
        }
    }
}
