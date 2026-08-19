using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShaloTrack_API.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VehicleShares",
                columns: table => new
                {
                    ShareId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedWithCustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InvitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleShares", x => x.ShareId);
                    table.ForeignKey(
                        name: "FK_VehicleShares_Customers_OwnerCustomerId",
                        column: x => x.OwnerCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleShares_Customers_SharedWithCustomerId",
                        column: x => x.SharedWithCustomerId,
                        principalTable: "Customers",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleShares_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleShares_OwnerCustomerId",
                table: "VehicleShares",
                column: "OwnerCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleShares_SharedWithCustomerId",
                table: "VehicleShares",
                column: "SharedWithCustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleShares_VehicleId",
                table: "VehicleShares",
                column: "VehicleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleShares");
        }
    }
}
